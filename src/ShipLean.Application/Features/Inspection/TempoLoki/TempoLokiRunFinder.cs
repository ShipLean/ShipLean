using System.Text.Json;
using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Linking;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// Finds the runs linked to one item (K3, D12): searches both stores for sessions that carry the item (tagged) or whose tool calls name
// it, reads each candidate session's spans and logs, and keeps the ones LinkRules links to the item. Spans come from Tempo and logs
// from Loki, merged by session id. Results are cached for ten minutes. Unreachable stores come back as Reachable=false.
[RegisterSingleton]
public sealed class TempoLokiRunFinder(TempoRunFinder tempo, LokiRunFinder loki, RunLinkCache cache, InspectionOptions options, TimeProvider time)
{
    private const int MaxRuns = 100;

    // The whole call, searches and fetches together, gives up after this long.
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

    public static readonly TimeSpan SearchLag = TimeSpan.FromMinutes(2);

    public async Task<RunSearch> FindAsync(RunQuery query, CancellationToken ct)
    {
        using var deadline = new CancellationTokenSource(Deadline, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            var cached = await cache.GetAsync(query.ProjectKey, query.ItemKey, ct);
            if (cached is not null)
            {
                return new RunSearch([.. cached.Where(run => run.EndedAt >= query.From && run.StartedAt <= query.To)], true, null);
            }
            return await SearchAsync(query, linked.Token);
        }
        catch (HttpRequestException problem)
        {
            return Unreachable(problem.Message);
        }
        catch (JsonException problem)
        {
            return Unreachable("The store answered with something that is not the expected JSON: " + problem.Message);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Unreachable(deadline.IsCancellationRequested ? $"Finding the runs took longer than {Deadline.TotalSeconds:0} seconds, so it was stopped." : "The store did not answer in time.");
        }
        catch (LokiSelectorException problem)
        {
            return Unreachable(problem.Message);
        }
    }

    private async Task<RunSearch> SearchAsync(RunQuery query, CancellationToken ct)
    {
        var scope = new FindScope();
        var found = await tempo.SessionsAsync(query, scope, ct);
        foreach (var session in await loki.SessionsAsync(query, scope, ct))
        {
            found.TryAdd(session, new HashSet<string>(StringComparer.Ordinal));
        }
        var runs = new List<RunSummary>();
        var traceIds = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var (session, discovered) in found.Take(MaxRuns))
        {
            var logs = await loki.LogsAsync(query, session, scope, ct);
            if (logs is null)
            {
                continue;
            }
            var spans = await tempo.SpansAsync(query, session, discovered.Concat(logs.Select(l => l.TraceId).OfType<string>()), scope, ct);
            if (spans is null || spans.Count + logs.Count == 0)
            {
                continue;
            }
            var summary = StoreRequests.Summarise(session, spans, logs, options.Fields.Item);
            if (summary.Links.Any(link => link.ItemKey == query.ItemKey))
            {
                runs.Add(summary);
                traceIds[session] = [.. spans.Select(s => s.TraceId).Distinct(StringComparer.Ordinal)];
            }
        }
        runs.Sort((a, b) => b.StartedAt.CompareTo(a.StartedAt));
        if (scope.Problems.Count > 0)
        {
            // A cut-short answer is not kept for ten minutes: the next ask tries again.
            return new RunSearch(runs, true, string.Join(" ", scope.Problems));
        }
        // A window that ends within two minutes of now is not kept: Tempo's search index lags ingestion, so a run linked only by a span can be missing yet.
        if (query.To <= time.GetUtcNow() - SearchLag)
        {
            await cache.SaveAsync(query.ProjectKey, query.ItemKey, runs, traceIds, ct);
        }
        return new RunSearch(runs, true, null);
    }

    private static RunSearch Unreachable(string problem) => new([], false, problem);
}
