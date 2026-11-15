using System.Globalization;
using System.Text;
using System.Text.Json;
using ShipLean.Application.Features.Inspection.Linking;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// What the Tempo and Loki parts share: one GET returning JSON, the unix times the two APIs take, regex-literal escaping for the key
// searches, and the OTLP value to string rule (K3 value normalisation).
internal static class StoreRequests
{
    // Null when the store answers 404 (a trace that is gone); any other failure throws HttpRequestException.
    public static async Task<JsonDocument?> GetJsonAsync(HttpClient client, string relativeUrl, string? tenantOverride, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        if (!string.IsNullOrWhiteSpace(tenantOverride))
        {
            request.Options.Set(InspectionHttp.TenantOverride, tenantOverride);
        }
        using var response = await client.SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
    }

    // A required member of a store's answer; a body of the wrong shape is a JsonException, which the sources report as an unreachable store.
    public static JsonElement Member(JsonElement owner, string name)
    {
        if (owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out var value))
        {
            return value;
        }
        throw new JsonException($"The answer has no '{name}' member.");
    }

    // An optional member; a body that is not an object has none.
    public static bool TryMember(JsonElement owner, string name, out JsonElement value)
    {
        value = default;
        return owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out value);
    }

    public static IEnumerable<JsonElement> Items(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("The answer has something other than a list where a list was expected.");
        }
        return array.EnumerateArray();
    }

    public static string Text(JsonElement value)
        => value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new JsonException("The answer has something other than text where text was expected.");

    public static long Nanos(JsonElement value)
        => long.TryParse(Text(value), NumberStyles.None, CultureInfo.InvariantCulture, out var nanos) ? nanos : throw new JsonException("The answer has a time that is not a number of nanoseconds.");

    public static long UnixSeconds(DateTimeOffset time) => time.ToUnixTimeSeconds();

    public static long UnixNanos(DateTimeOffset time) => (time.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100;

    public static DateTimeOffset FromNanos(long nanos) => new(DateTimeOffset.UnixEpoch.UtcTicks + (nanos / 100), TimeSpan.Zero);

    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    // A regex that matches the text anywhere (the stores anchor regexes): .* around the literal, every ASCII punctuation character escaped.
    public static string ContainsPattern(string literal)
    {
        var text = new StringBuilder("(?s).*");
        foreach (var c in literal)
        {
            text.Append(Literal(c));
        }
        return text.Append(".*").ToString();
    }

    // Letters, digits, '_' and anything non-ASCII stand for themselves; other ASCII punctuation is escaped; spaces and controls cannot be escaped, so they match any character.
    private static string Literal(char c)
    {
        if (char.IsAsciiLetterOrDigit(c) || c == '_' || c > 127)
        {
            return c.ToString();
        }
        return char.IsWhiteSpace(c) || char.IsControl(c) ? "." : "\\" + c;
    }

    // The K3 string form of an OTLP/JSON AnyValue: ints and doubles invariant, bools true|false, arrays as compact JSON.
    // OTLP/JSON writes an int64 as a string; a plain number is accepted too.
    private static string IntText(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return Text(value);
        }
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var integer) ? integer.ToString(CultureInfo.InvariantCulture) : throw WrongKind();
    }

    private static JsonException WrongKind() => new("The answer has a value of the wrong kind.");

    public static string? ValueText(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("The answer has a value of the wrong shape.");
        }
        foreach (var property in value.EnumerateObject())
        {
            switch (property.Name)
            {
                case "stringValue":
                    return Text(property.Value);
                case "intValue":
                    return IntText(property.Value);
                case "doubleValue":
                    return property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var real) ? real.ToString("R", CultureInfo.InvariantCulture) : throw WrongKind();
                case "boolValue":
                    return property.Value.ValueKind switch
                    {
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => throw WrongKind(),
                    };
                case "arrayValue":
                    return ArrayText(property.Value);
                default:
                    return null;
            }
        }
        return null;
    }

    private static string ArrayText(JsonElement array)
    {
        var items = TryMember(array, "values", out var values) ? Items(values).Select(ValueText).ToList() : [];
        return JsonSerializer.Serialize(items);
    }

    public static IReadOnlyDictionary<string, string> Attributes(JsonElement owner)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (TryMember(owner, "attributes", out var attributes))
        {
            foreach (var attribute in Items(attributes))
            {
                var text = ValueText(Member(attribute, "value"));
                if (text is not null)
                {
                    map[Text(Member(attribute, "key"))] = text;
                }
            }
        }
        return map;
    }

    // Distinct models, token sums, error count and the time range of what the stores returned for one session, as the fixtures' README pins them.
    public static RunSummary Summarise(string sessionId, IReadOnlyList<SpanRecord> spans, IReadOnlyList<LogRecordData> logs, string itemField)
    {
        var attributes = spans.Select(s => s.Attributes).Concat(logs.Select(l => l.Attributes)).ToList();
        var times = spans.SelectMany(s => new[] { s.Start, s.End }).Concat(logs.Select(l => l.Time)).ToList();
        return new RunSummary(
            sessionId,
            sessionId,
            First(attributes, "service.name") ?? string.Empty,
            First(attributes, "user.email"),
            [.. attributes.Select(a => a.GetValueOrDefault("model")).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            times.Min(),
            times.Max(),
            spans.Count,
            logs.Count,
            spans.Count(s => s.Status == SpanStatus.Error) + logs.Count(l => l.Severity == "ERROR"),
            Tokens(spans.Select(s => s.Attributes), logs.Select(l => l.Attributes), "input_tokens"),
            Tokens(spans.Select(s => s.Attributes), logs.Select(l => l.Attributes), "output_tokens"),
            LinkRules.Derive(spans, logs, itemField));
    }

    private static string? First(List<IReadOnlyDictionary<string, string>> attributes, string key) => attributes.Select(a => a.GetValueOrDefault(key)).FirstOrDefault(v => v is not null);

    // Summed on spans when any span has the attribute, else on logs, else null.
    private static long? Tokens(IEnumerable<IReadOnlyDictionary<string, string>> spans, IEnumerable<IReadOnlyDictionary<string, string>> logs, string key)
        => Sum(spans, key) ?? Sum(logs, key);

    private static long? Sum(IEnumerable<IReadOnlyDictionary<string, string>> attributes, string key)
    {
        var values = attributes.Select(a => a.GetValueOrDefault(key)).OfType<string>().Select(v => long.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : throw new JsonException("A token count is not a whole number.")).ToList();
        return values.Count == 0 ? null : values.Sum();
    }
}
