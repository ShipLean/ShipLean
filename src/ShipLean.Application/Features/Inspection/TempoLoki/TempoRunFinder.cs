using System.Text.Json;
using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Query;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// The Tempo half of finding runs (K3): TraceQL searches for the traces that name the item, the sessions those traces belong to,
// and the spans of a session. Reads only; every search carries the project filter and a time window.
[RegisterSingleton]
public sealed class TempoRunFinder(IHttpClientFactory clients, InspectionOptions options)
{
    // Tempo refuses a search wider than its max_duration (168 h by default), so a wider window goes in chunks.
    private static readonly TimeSpan SearchChunk = TimeSpan.FromDays(6);
    private const int SearchLimit = 200;

    // Session ids of the traces tagged with the item, or holding a call whose input names it. Not yet verified against LinkRules.
    public async Task<IReadOnlyList<string>> SessionsAsync(RunQuery query, TraceCache cache, CancellationToken ct)
    {
        var project = $"resource.{options.Fields.Project} = {QueryText.TraceQlString(query.ProjectKey)}";
        var tagged = $"{{ {project} && resource.{options.Fields.Item} = {QueryText.TraceQlString(query.ItemKey)} }}";
        var pattern = QueryText.TraceQlString(StoreRequests.ContainsPattern(query.ItemKey));
        var called = $"{{ {project} && (span.tool_parameters =~ {pattern} || span.tool_input =~ {pattern} || span.input =~ {pattern}) }}";
        var sessions = new List<string>();
        foreach (var traceQl in new[] { tagged, called })
        {
            foreach (var traceId in await SearchAsync(traceQl, query.From, query.To, query.TenantOverride, ct))
            {
                var spans = await cache.GetAsync(traceId, () => FetchAsync(traceId, query.TenantOverride, ct));
                sessions.AddRange(spans.Select(s => s.Attributes.GetValueOrDefault(options.Fields.Session)).OfType<string>().Take(1));
            }
        }
        return [.. sessions.Distinct(StringComparer.Ordinal)];
    }

    // Every span of the session in the project and window: all its traces.
    public async Task<IReadOnlyList<SpanRecord>> SpansAsync(RunQuery query, string sessionId, TraceCache cache, CancellationToken ct)
    {
        var traceQl = $"{{ resource.{options.Fields.Project} = {QueryText.TraceQlString(query.ProjectKey)} && span.{options.Fields.Session} = {QueryText.TraceQlString(sessionId)} }}";
        var spans = new List<SpanRecord>();
        foreach (var traceId in await SearchAsync(traceQl, query.From, query.To, query.TenantOverride, ct))
        {
            spans.AddRange(await cache.GetAsync(traceId, () => FetchAsync(traceId, query.TenantOverride, ct)));
        }
        return spans;
    }

    private async Task<IReadOnlyList<string>> SearchAsync(string traceQl, DateTimeOffset from, DateTimeOffset to, string? tenant, CancellationToken ct)
    {
        var client = clients.CreateClient(InspectionHttp.TempoClient);
        var found = new List<string>();
        for (var start = from; start < to; start += SearchChunk)
        {
            var end = start + SearchChunk < to ? start + SearchChunk : to;
            var url = $"api/search?q={Uri.EscapeDataString(traceQl)}&start={StoreRequests.UnixSeconds(start)}&end={StoreRequests.UnixSeconds(end) + 1}&limit={SearchLimit}";
            using var document = await StoreRequests.GetJsonAsync(client, url, tenant, ct);
            if (document is not null && document.RootElement.TryGetProperty("traces", out var traces))
            {
                found.AddRange(traces.EnumerateArray().Select(t => t.GetProperty("traceID").GetString()!));
            }
        }
        return [.. found.Distinct(StringComparer.Ordinal)];
    }

    // One trace as spans, each with its resource's attributes merged in (the span's own winning), all assigned to the session the trace carries.
    private async Task<IReadOnlyList<SpanRecord>> FetchAsync(string traceId, string? tenant, CancellationToken ct)
    {
        using var document = await StoreRequests.GetJsonAsync(clients.CreateClient(InspectionHttp.TempoClient), $"api/traces/{traceId}", tenant, ct);
        if (document is null)
        {
            return [];
        }
        var batches = document.RootElement.TryGetProperty("batches", out var b) ? b : document.RootElement.GetProperty("resourceSpans");
        var spans = new List<SpanRecord>();
        foreach (var batch in batches.EnumerateArray())
        {
            var resource = batch.TryGetProperty("resource", out var r) ? StoreRequests.Attributes(r) : new Dictionary<string, string>();
            foreach (var span in batch.GetProperty("scopeSpans").EnumerateArray().SelectMany(s => s.GetProperty("spans").EnumerateArray()))
            {
                spans.Add(Span(traceId, span, resource));
            }
        }
        var session = spans.Select(s => s.Attributes.GetValueOrDefault(options.Fields.Session)).FirstOrDefault(v => v is not null);
        return session is null ? spans : [.. spans.Select(s => s.Attributes.ContainsKey(options.Fields.Session) ? s : s with { Attributes = new Dictionary<string, string>(s.Attributes) { [options.Fields.Session] = session } })];
    }

    private static SpanRecord Span(string traceId, JsonElement span, IReadOnlyDictionary<string, string> resource)
    {
        var attributes = new Dictionary<string, string>(resource, StringComparer.Ordinal);
        foreach (var (key, value) in StoreRequests.Attributes(span))
        {
            attributes[key] = value;
        }
        var status = span.TryGetProperty("status", out var s) && s.TryGetProperty("code", out var code) ? code : default;
        return new SpanRecord(
            traceId,
            span.GetProperty("spanId").GetString()!,
            span.TryGetProperty("parentSpanId", out var parent) ? parent.GetString() : null,
            span.GetProperty("name").GetString()!,
            "internal",
            Time(span, "startTimeUnixNano"),
            Time(span, "endTimeUnixNano"),
            IsError(status) ? SpanStatus.Error : SpanStatus.Unset,
            null,
            attributes,
            []);
    }

    private static bool IsError(JsonElement code)
        => code.ValueKind == JsonValueKind.String ? code.GetString() == "STATUS_CODE_ERROR" : code.ValueKind == JsonValueKind.Number && code.GetInt32() == 2;

    private static DateTimeOffset Time(JsonElement span, string name) => StoreRequests.FromNanos(long.Parse(span.GetProperty(name).GetString()!, System.Globalization.CultureInfo.InvariantCulture));
}

// Traces fetched during one search, so a trace named by two searches is read once.
public sealed class TraceCache
{
    private readonly Dictionary<string, IReadOnlyList<SpanRecord>> traces = [];

    public async Task<IReadOnlyList<SpanRecord>> GetAsync(string traceId, Func<Task<IReadOnlyList<SpanRecord>>> fetch)
    {
        if (!traces.TryGetValue(traceId, out var spans))
        {
            spans = await fetch();
            traces[traceId] = spans;
        }
        return spans;
    }
}
