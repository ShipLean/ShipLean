using System.Text.Json;
using Agoda.IoC.Core;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

// One run's spans (Tempo) and logs (Loki) in the project and window, as K3 normalises them. The run is served only under an item it
// links to, by the same LinkRules the finder uses (StoreRequests.Summarise). Null when nothing remains, when the run does not link to
// the item, or when a store cannot be read: the port never throws.
[RegisterSingleton]
public sealed class TempoLokiRunReader(TempoTraceLoader tempo, LokiLogReader loki, InspectionOptions options, TimeProvider time)
{
    public async Task<RawRun?> GetAsync(RunRef run, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(run.RunId))
        {
            return null;
        }
        using var deadline = new CancellationTokenSource(TempoLokiRunFinder.Deadline, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try
        {
            var query = new RunQuery(run.ProjectKey, run.ItemKey, run.From, run.To, run.TenantOverride, run.SelectorOverride);
            var logs = await loki.LoadAsync(query, run.RunId, linked.Token);
            var traces = await tempo.LoadAsync(query, run.RunId, logs.Logs.Select(l => l.TraceId).OfType<string>(), linked.Token);
            if (traces.Spans.Count + logs.Logs.Count == 0)
            {
                return null;
            }
            var summary = StoreRequests.Summarise(run.RunId, traces.Spans, logs.Logs, options.Fields.Item);
            return summary.Links.Any(link => link.ItemKey == run.ItemKey) ? new RawRun(summary, traces.Spans, logs.Logs, traces.Truncated || logs.Truncated) : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (LokiSelectorException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
