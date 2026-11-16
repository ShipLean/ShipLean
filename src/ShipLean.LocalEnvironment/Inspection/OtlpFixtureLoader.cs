using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShipLean.LocalEnvironment.Inspection;

// Sends an OTLP/JSON fixture to the collector, the only way anything reaches the stores (ShipLean has no OTLP endpoint). Times are
// rebased so the run starts at `now`; shiplean.project becomes the test's own key; trace ids and session ids are renamed per key
// so a fixture loaded again under another key (or in another run, on reused containers) never merges with earlier data.
public static class OtlpFixtureLoader
{
    private const string ProjectAttribute = "shiplean.project";
    private const string SessionAttribute = "session.id";
    private static readonly HttpClient Http = new();

    // otlpPath is the fixture's <name>.otlp.json. batches > 1 posts the fixture's resourceSpans/resourceLogs entries in that many
    // separate requests (entry i goes to request i % batches).
    public static async Task<LoadedFixture> SendAsync(
        string collectorUrl, string otlpPath, string projectKey, DateTimeOffset now, int batches = 1, CancellationToken ct = default)
    {
        var root = JsonNode.Parse(await File.ReadAllTextAsync(otlpPath, ct))!.AsObject();
        var traceIds = new SortedSet<string>(StringComparer.Ordinal);
        var sessionIds = new SortedSet<string>(StringComparer.Ordinal);
        var earliest = long.MaxValue;
        string? originalProject = null;
        Walk(root, node =>
        {
            foreach (var (name, value) in node)
            {
                switch (name)
                {
                    case "traceId" when value?.GetValue<string>() is { Length: > 0 } id:
                        traceIds.Add(id);
                        break;
                    case "timeUnixNano" or "startTimeUnixNano" when value is not null:
                        earliest = Math.Min(earliest, long.Parse(value.GetValue<string>()));
                        break;
                }
            }
            if (node["key"]?.GetValue<string>() is { } key && node["value"]?["stringValue"]?.GetValue<string>() is { } text)
            {
                if (key == ProjectAttribute)
                {
                    originalProject ??= text;
                }
                else if (key == SessionAttribute)
                {
                    sessionIds.Add(text);
                }
            }
        });
        var loaded = new LoadedFixture(projectKey, originalProject!, traceIds.ToDictionary(t => t, t => NewTraceId(projectKey, t)),
            sessionIds.ToDictionary(s => s, s => $"{s}-{projectKey}"), now);

        var shift = (now.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / 10 * 1000 - earliest;
        Walk(root, node =>
        {
            foreach (var (name, value) in node.ToList())
            {
                if (name.EndsWith("UnixNano", StringComparison.Ordinal) && value is not null)
                {
                    node[name] = (long.Parse(value.GetValue<string>()) + shift).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }
            if (node["key"]?.GetValue<string>() == ProjectAttribute && node["value"] is JsonObject projectValue)
            {
                projectValue["stringValue"] = projectKey;
            }
        });
        var text = loaded.Rewrite(root.ToJsonString());
        root = JsonNode.Parse(text)!.AsObject();

        for (var batch = 0; batch < batches; batch++)
        {
            await PostAsync(collectorUrl, "/v1/traces", "resourceSpans", Pick(root, "resourceSpans", batch, batches), ct);
            await PostAsync(collectorUrl, "/v1/logs", "resourceLogs", Pick(root, "resourceLogs", batch, batches), ct);
        }
        return loaded;
    }

    private static JsonArray Pick(JsonObject root, string name, int batch, int batches)
    {
        var all = root[name]?.AsArray() ?? [];
        var picked = new JsonArray();
        for (var i = batch; i < all.Count; i += batches)
        {
            picked.Add(all[i]!.DeepClone());
        }
        return picked;
    }

    private static async Task PostAsync(string collectorUrl, string path, string property, JsonArray entries, CancellationToken ct)
    {
        if (entries.Count == 0)
        {
            return;
        }
        var body = new JsonObject { [property] = entries }.ToJsonString();
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(collectorUrl.TrimEnd('/') + path, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"The collector answered {(int)response.StatusCode} to POST {path}: {await response.Content.ReadAsStringAsync(ct)}");
        }
    }

    private static string NewTraceId(string projectKey, string original)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(projectKey + "|" + original)))[..32].ToLowerInvariant();

    private static void Walk(JsonNode? node, Action<JsonObject> visit)
    {
        switch (node)
        {
            case JsonObject obj:
                visit(obj);
                foreach (var (_, child) in obj.ToList())
                {
                    Walk(child, visit);
                }
                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Walk(child, visit);
                }
                break;
        }
    }
}

// What a load did to a fixture: the new project key, trace ids and session ids. Rewrite applies the same renaming to the
// fixture's expected RawRun JSON, so the store's answer can be compared with it.
public sealed record LoadedFixture(
    string ProjectKey,
    string OriginalProjectKey,
    IReadOnlyDictionary<string, string> TraceIds,
    IReadOnlyDictionary<string, string> SessionIds,
    DateTimeOffset StartedAt)
{
    // Quoted JSON strings only: an id inside a longer value is left alone.
    public string Rewrite(string json)
    {
        foreach (var (old, renamed) in TraceIds.Concat(SessionIds))
        {
            json = json.Replace(Quote(old), Quote(renamed), StringComparison.Ordinal);
        }
        return json.Replace(
            $"{Quote(ProjectAttributeName)}:{Quote(OriginalProjectKey)}", $"{Quote(ProjectAttributeName)}:{Quote(ProjectKey)}", StringComparison.Ordinal);
    }

    private const string ProjectAttributeName = "shiplean.project";

    private static string Quote(string value) => JsonSerializer.Serialize(value);
}
