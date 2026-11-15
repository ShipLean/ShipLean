using System.Text.Json;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// Tempo's trace-by-id answer (OTLP/JSON) as normalised spans (K3). Tempo writes span ids base64 (a protobuf bytes field in JSON), where
// the rest of the world, and the log records' span_id, use lower-case hex; the trace id is the one that was asked for.
internal static class OtlpJsonMapping
{
    private const int HexSpanIdLength = 16;

    // The spans of one trace answer ({ trace: { resourceSpans } }, or the older { batches } / { resourceSpans }), each with its
    // resource's attributes merged in and the span's own winning.
    public static IReadOnlyList<SpanRecord> Spans(JsonElement answer, string traceId)
    {
        var root = StoreRequests.TryMember(answer, "trace", out var trace) ? trace : answer;
        var batches = StoreRequests.TryMember(root, "resourceSpans", out var resourceSpans) ? resourceSpans : StoreRequests.Member(root, "batches");
        var spans = new List<SpanRecord>();
        foreach (var batch in StoreRequests.Items(batches))
        {
            var resource = StoreRequests.TryMember(batch, "resource", out var r) ? StoreRequests.Attributes(r) : new Dictionary<string, string>();
            foreach (var scope in StoreRequests.Items(StoreRequests.Member(batch, "scopeSpans")))
            {
                spans.AddRange(StoreRequests.Items(StoreRequests.Member(scope, "spans")).Select(span => Span(traceId, span, resource)));
            }
        }
        return spans;
    }

    // True when Tempo says the trace it sent is not all of it.
    public static bool IsPartial(JsonElement answer)
        => StoreRequests.TryMember(answer, "status", out var status) && status.ValueKind == JsonValueKind.String && status.GetString() == "partial";

    private static SpanRecord Span(string traceId, JsonElement span, IReadOnlyDictionary<string, string> resource)
    {
        var attributes = new Dictionary<string, string>(resource, StringComparer.Ordinal);
        foreach (var (key, value) in StoreRequests.Attributes(span))
        {
            attributes[key] = value;
        }
        var status = StoreRequests.TryMember(span, "status", out var s) ? s : default;
        return new SpanRecord(
            traceId,
            SpanId(StoreRequests.Member(span, "spanId")) ?? throw new JsonException("A span has an empty id."),
            StoreRequests.TryMember(span, "parentSpanId", out var parent) ? SpanId(parent) : null,
            StoreRequests.Text(StoreRequests.Member(span, "name")),
            Kind(span),
            Time(span, "startTimeUnixNano"),
            Time(span, "endTimeUnixNano"),
            StatusCode(status),
            StoreRequests.TryMember(status, "message", out var message) && message.ValueKind == JsonValueKind.String && message.GetString() is { Length: > 0 } text ? text : null,
            attributes,
            Events(span));
    }

    private static List<SpanEventData> Events(JsonElement span)
    {
        if (!StoreRequests.TryMember(span, "events", out var events))
        {
            return [];
        }
        return [.. StoreRequests.Items(events).Select(e => new SpanEventData(Time(e, "timeUnixNano"), StoreRequests.Text(StoreRequests.Member(e, "name")), StoreRequests.Attributes(e)))];
    }

    // Null for a missing or empty id (a root span has no parent).
    private static string? SpanId(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
        {
            return null;
        }
        var id = value.GetString()!;
        if (id.Length == HexSpanIdLength)
        {
            return id.ToLowerInvariant();
        }
        try
        {
            return Convert.ToHexStringLower(Convert.FromBase64String(id));
        }
        catch (FormatException)
        {
            throw new JsonException("A span id is neither hex nor base64.");
        }
    }

    // An OTLP enum is its name or its number; anything else is a body of the wrong shape.
    private static string EnumName(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString()!,
        JsonValueKind.Number when value.TryGetInt32(out var number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => throw new JsonException("A span has a kind or status code of the wrong kind."),
    };

    // SPAN_KIND_CLIENT or its number; unspecified is internal.
    private static string Kind(JsonElement span)
    {
        if (!StoreRequests.TryMember(span, "kind", out var kind))
        {
            return "internal";
        }
        var name = EnumName(kind);
        return name switch
        {
            "2" or "SPAN_KIND_SERVER" => "server",
            "3" or "SPAN_KIND_CLIENT" => "client",
            "4" or "SPAN_KIND_PRODUCER" => "producer",
            "5" or "SPAN_KIND_CONSUMER" => "consumer",
            _ => "internal",
        };
    }

    private static SpanStatus StatusCode(JsonElement status)
    {
        if (!StoreRequests.TryMember(status, "code", out var code))
        {
            return SpanStatus.Unset;
        }
        var name = EnumName(code);
        return name switch
        {
            "1" or "STATUS_CODE_OK" => SpanStatus.Ok,
            "2" or "STATUS_CODE_ERROR" => SpanStatus.Error,
            _ => SpanStatus.Unset,
        };
    }

    // Microsecond precision, UTC.
    private static DateTimeOffset Time(JsonElement owner, string name) => StoreRequests.FromNanos(StoreRequests.Nanos(StoreRequests.Member(owner, name)) / 1000 * 1000);
}
