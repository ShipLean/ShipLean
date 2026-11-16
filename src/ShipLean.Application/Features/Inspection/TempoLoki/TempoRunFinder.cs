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

    // The sessions of the traces tagged with the item, or holding a call whose input names it, each with the trace ids that led to it. The
    // session comes from the search itself (select), so no trace is fetched for it, unless a hit shows none; then that trace is fetched
    // once (within the call's fetch cap). Not yet verified against LinkRules.
    public async Task<Dictionary<string, HashSet<string>>> SessionsAsync(RunQuery query, FindScope scope, CancellationToken ct)
    {
        var project = $"resource.{options.Fields.Project} = {QueryText.TraceQlString(query.ProjectKey)}";
        var tagged = $"{{ {project} && .{options.Fields.Item} = {QueryText.TraceQlString(query.ItemKey)} }}";
        var pattern = QueryText.TraceQlString(StoreRequests.ContainsPattern(query.ItemKey));
        var called = $"{{ {project} && (span.tool_parameters =~ {pattern} || span.tool_input =~ {pattern} || span.input =~ {pattern}) }}";
        var sessions = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var traceQl in new[] { tagged, called })
        {
            foreach (var hit in await SearchAsync(traceQl, query.From, query.To, query.TenantOverride, scope, ct))
            {
                var session = hit.Session;
                if (session is null)
                {
                    var spans = await scope.GetAsync(hit.TraceId, () => FetchAsync(hit.TraceId, query.TenantOverride, ct));
                    session = spans?.Select(s => s.Attributes.GetValueOrDefault(options.Fields.Session)).FirstOrDefault(v => v is not null);
                }
                if (session is not null)
                {
                    sessions.TryAdd(session, new HashSet<string>(StringComparer.Ordinal));
                    sessions[session].Add(hit.TraceId);
                }
            }
        }
        return sessions;
    }

    // Every span of the session in the project and window. Tempo's search index lags ingestion and can be partial, so search only finds
    // candidate trace ids: the ones that led to the session, the ones the session's log records carry and the ones its own search names
    // are all fetched by id for the full spans. Spans of another project are dropped (fetch by id does not filter). Null when the call's
    // fetch cap stopped the read, so the session would be summarised from part of its spans.
    public async Task<IReadOnlyList<SpanRecord>?> SpansAsync(RunQuery query, string sessionId, IEnumerable<string> knownTraceIds, FindScope scope, CancellationToken ct)
    {
        var traceQl = $"{{ resource.{options.Fields.Project} = {QueryText.TraceQlString(query.ProjectKey)} && .{options.Fields.Session} = {QueryText.TraceQlString(sessionId)} }}";
        var candidates = knownTraceIds.Concat((await SearchAsync(traceQl, query.From, query.To, query.TenantOverride, scope, ct)).Select(h => h.TraceId)).Distinct(StringComparer.Ordinal);
        var spans = new List<SpanRecord>();
        foreach (var traceId in candidates)
        {
            var fetched = await scope.GetAsync(traceId, () => FetchAsync(traceId, query.TenantOverride, ct));
            if (fetched is null)
            {
                return null;
            }
            spans.AddRange(fetched.Where(s => s.Attributes.GetValueOrDefault(options.Fields.Project) == query.ProjectKey));
        }
        return spans;
    }

    // The trace ids a search returns, each with the session the search's select reported for it, if any.
    private async Task<IReadOnlyList<Hit>> SearchAsync(string traceQl, DateTimeOffset from, DateTimeOffset to, string? tenant, FindScope scope, CancellationToken ct)
    {
        var client = clients.CreateClient(InspectionHttp.TempoClient);
        var found = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var start = from; start < to; start += SearchChunk)
        {
            var end = start + SearchChunk < to ? start + SearchChunk : to;
            var url = $"api/search?q={Uri.EscapeDataString(traceQl + " | select(." + options.Fields.Session + ")")}&start={StoreRequests.UnixSeconds(start)}&end={StoreRequests.UnixSeconds(end) + 1}&limit={SearchLimit}";
            using var document = await StoreRequests.GetJsonAsync(client, url, tenant, ct) ?? throw new HttpRequestException("Tempo answered 404 to a search.");
            if (!StoreRequests.TryMember(document.RootElement, "traces", out var traces))
            {
                continue;
            }
            var count = 0;
            foreach (var trace in StoreRequests.Items(traces))
            {
                count++;
                var id = StoreRequests.Text(StoreRequests.Member(trace, "traceID"));
                found[id] = found.GetValueOrDefault(id) ?? SessionOf(trace);
            }
            if (count >= SearchLimit)
            {
                scope.Note($"Tempo returned {SearchLimit} traces for one search window, which is its limit; older runs of this item may be missing.");
            }
        }
        return [.. found.Select(f => new Hit(f.Key, f.Value))];
    }

    private string? SessionOf(JsonElement trace)
    {
        var sets = new List<JsonElement>();
        if (StoreRequests.TryMember(trace, "spanSets", out var many) && many.ValueKind == JsonValueKind.Array)
        {
            sets.AddRange(many.EnumerateArray());
        }
        else if (StoreRequests.TryMember(trace, "spanSet", out var one))
        {
            sets.Add(one);
        }
        return sets
            .Where(set => StoreRequests.TryMember(set, "spans", out _))
            .SelectMany(set => StoreRequests.Items(StoreRequests.Member(set, "spans")))
            .Select(span => StoreRequests.Attributes(span).GetValueOrDefault(options.Fields.Session))
            .FirstOrDefault(v => v is not null);
    }

    private sealed record Hit(string TraceId, string? Session);

    // One trace as spans, each with its resource's attributes merged in (the span's own winning), all assigned to the session the trace carries.
    private async Task<IReadOnlyList<SpanRecord>> FetchAsync(string traceId, string? tenant, CancellationToken ct)
    {
        using var document = await StoreRequests.GetJsonAsync(clients.CreateClient(InspectionHttp.TempoClient), $"api/traces/{traceId}", tenant, ct);
        if (document is null)
        {
            return [];
        }
        var batches = StoreRequests.TryMember(document.RootElement, "batches", out var b) ? b : StoreRequests.Member(document.RootElement, "resourceSpans");
        var spans = new List<SpanRecord>();
        foreach (var batch in StoreRequests.Items(batches))
        {
            var resource = StoreRequests.TryMember(batch, "resource", out var r) ? StoreRequests.Attributes(r) : new Dictionary<string, string>();
            foreach (var span in StoreRequests.Items(StoreRequests.Member(batch, "scopeSpans")).SelectMany(s => StoreRequests.Items(StoreRequests.Member(s, "spans"))))
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
        var status = StoreRequests.TryMember(span, "status", out var s) && StoreRequests.TryMember(s, "code", out var code) ? code : default;
        return new SpanRecord(
            traceId,
            StoreRequests.Text(StoreRequests.Member(span, "spanId")),
            StoreRequests.TryMember(span, "parentSpanId", out var parent) && parent.ValueKind == JsonValueKind.String ? parent.GetString() : null,
            StoreRequests.Text(StoreRequests.Member(span, "name")),
            "internal",
            Time(span, "startTimeUnixNano"),
            Time(span, "endTimeUnixNano"),
            IsError(status) ? SpanStatus.Error : SpanStatus.Unset,
            null,
            attributes,
            []);
    }

    private static bool IsError(JsonElement code)
        => code.ValueKind == JsonValueKind.String ? code.GetString() == "STATUS_CODE_ERROR" : code.ValueKind == JsonValueKind.Number && code.TryGetInt32(out var number) && number == 2;

    private static DateTimeOffset Time(JsonElement span, string name) => StoreRequests.FromNanos(StoreRequests.Nanos(StoreRequests.Member(span, name)));
}
