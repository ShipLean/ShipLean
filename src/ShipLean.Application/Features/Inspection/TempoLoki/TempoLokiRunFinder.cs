using System.Text.Json;
using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Linking;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// Finds the runs linked to one item (K3, D12): searches both stores for sessions that carry the item (tagged) or whose tool calls name
// it, reads each candidate session's spans and logs, and keeps the ones LinkRules links to the item. Spans come from Tempo and logs
// from Loki, merged by session id. Results are cached for ten minutes. Unreachable stores come back as Reachable=false.
[RegisterSingleton]
public sealed class TempoLokiRunFinder(TempoRunFinder tempo, LokiRunFinder loki, RunLinkCache cache, InspectionOptions options)
{
    private const int MaxRuns = 100;

    public async Task<RunSearch> FindAsync(RunQuery query, CancellationToken ct)
    {
        try
        {
            var cached = await cache.GetAsync(query.ProjectKey, query.ItemKey, ct);
            if (cached is not null)
            {
                return new RunSearch([.. cached.Where(run => run.EndedAt >= query.From && run.StartedAt <= query.To)], true, null);
            }
            return await SearchAsync(query, ct);
        }
        catch (HttpRequestException problem)
        {
            return Unreachable(problem.Message);
        }
        catch (JsonException problem)
        {
            return Unreachable("The store answered with something that is not the expected JSON: " + problem.Message);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Unreachable("The store did not answer in time.");
        }
        catch (ArgumentException problem)
        {
            return Unreachable(problem.Message);
        }
    }

    private async Task<RunSearch> SearchAsync(RunQuery query, CancellationToken ct)
    {
        var traces = new TraceCache();
        var sessions = (await tempo.SessionsAsync(query, traces, ct)).Concat(await loki.SessionsAsync(query, ct)).Distinct(StringComparer.Ordinal).Take(MaxRuns).ToList();
        var runs = new List<RunSummary>();
        var traceIds = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var session in sessions)
        {
            var spans = await tempo.SpansAsync(query, session, traces, ct);
            var logs = await loki.LogsAsync(query, session, ct);
            if (spans.Count + logs.Count == 0)
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
        await cache.SaveAsync(query.ProjectKey, query.ItemKey, runs, traceIds, ct);
        return new RunSearch(runs, true, null);
    }

    private static RunSearch Unreachable(string problem) => new([], false, problem);
}
