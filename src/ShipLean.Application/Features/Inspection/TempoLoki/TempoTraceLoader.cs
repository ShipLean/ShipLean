using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Query;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

public sealed record TraceLoad(IReadOnlyList<SpanRecord> Spans, bool Truncated);

// Loads one session's spans from Tempo (K3). Search only names candidate traces: Tempo's index lags ingestion and can be partial, so the
// trace ids the session's logs carry are candidates too, and every candidate is fetched by id for its full spans. A fetch by id does not
// filter, so spans of another project or session are dropped here. Never reads the finder's cache.
[RegisterSingleton]
public sealed class TempoTraceLoader(IHttpClientFactory clients, InspectionOptions options)
{
    // Tempo refuses a search wider than its max_duration (168 h by default), so a wider window goes in chunks.
    private static readonly TimeSpan SearchChunk = TimeSpan.FromDays(6);
    private const int SearchLimit = 200;

    // The most traces one run read fetches; more than this and the run comes back truncated.
    public const int MaxTraces = FindScope.MaxTraces;

    public async Task<TraceLoad> LoadAsync(RunQuery query, string sessionId, IEnumerable<string> knownTraceIds, CancellationToken ct)
    {
        var truncated = false;
        var candidates = new List<string>(knownTraceIds.Distinct(StringComparer.Ordinal));
        var found = await SearchAsync(query, sessionId, ct);
        truncated |= found.Truncated;
        candidates.AddRange(found.TraceIds.Where(id => !candidates.Contains(id, StringComparer.Ordinal)));
        if (candidates.Count > MaxTraces)
        {
            truncated = true;
            candidates = candidates.Take(MaxTraces).ToList();
        }
        var spans = new List<SpanRecord>();
        foreach (var traceId in candidates)
        {
            var fetched = await FetchAsync(traceId, query, ct);
            truncated |= fetched.Truncated;
            // Spans without a session attribute are kept on purpose: session.id may sit on only some spans of a trace.
            spans.AddRange(fetched.Spans.Where(s => s.Start <= query.To && s.End >= query.From).Where(s => s.Attributes.GetValueOrDefault(options.Fields.Project) == query.ProjectKey
                && s.Attributes.GetValueOrDefault(options.Fields.Session, sessionId) == sessionId));
        }
        return new TraceLoad([.. spans.OrderBy(s => s.Start).ThenBy(s => s.SpanId, StringComparer.Ordinal)], truncated);
    }

    private async Task<(List<string> TraceIds, bool Truncated)> SearchAsync(RunQuery query, string sessionId, CancellationToken ct)
    {
        var traceQl = $"{{ .{options.Fields.Project} = {QueryText.TraceQlString(query.ProjectKey)} && .{options.Fields.Session} = {QueryText.TraceQlString(sessionId)} }}";
        var client = clients.CreateClient(InspectionHttp.TempoClient);
        var ids = new List<string>();
        var truncated = false;
        for (var start = query.From; start < query.To; start += SearchChunk)
        {
            var end = start + SearchChunk < query.To ? start + SearchChunk : query.To;
            var url = $"api/search?q={Uri.EscapeDataString(traceQl)}&start={StoreRequests.UnixSeconds(start)}&end={StoreRequests.UnixSeconds(end) + 1}&limit={SearchLimit}";
            using var document = await StoreRequests.GetJsonAsync(client, url, query.TenantOverride, ct) ?? throw new HttpRequestException("Tempo answered 404 to a search.");
            if (!StoreRequests.TryMember(document.RootElement, "traces", out var traces))
            {
                continue;
            }
            var hits = StoreRequests.Items(traces).Select(t => StoreRequests.Text(StoreRequests.Member(t, "traceID"))).ToList();
            truncated |= hits.Count >= SearchLimit;
            ids.AddRange(hits.Where(id => !ids.Contains(id, StringComparer.Ordinal)));
        }
        return (ids, truncated);
    }

    // A trace that is gone (404) has no spans.
    private async Task<TraceLoad> FetchAsync(string traceId, RunQuery query, CancellationToken ct)
    {
        var url = $"api/v2/traces/{Uri.EscapeDataString(traceId)}?start={StoreRequests.UnixSeconds(query.From)}&end={StoreRequests.UnixSeconds(query.To) + 1}";
        using var document = await StoreRequests.GetJsonAsync(clients.CreateClient(InspectionHttp.TempoClient), url, query.TenantOverride, ct);
        return document is null ? new TraceLoad([], false) : new TraceLoad(OtlpJsonMapping.Spans(document.RootElement, traceId), OtlpJsonMapping.IsPartial(document.RootElement));
    }
}
