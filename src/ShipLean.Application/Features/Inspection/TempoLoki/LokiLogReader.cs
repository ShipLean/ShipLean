using Agoda.IoC.Core;
using ShipLean.Application.Features.Inspection.Query;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.TempoLoki;

public sealed record LogLoad(IReadOnlyList<LogRecordData> Logs, bool Truncated);

// Loads one session's log records from Loki (K3), by the session field's Loki name (the K1 name with '.' as '_'), inside the project
// filter and the window. Parsing and the mapping of label names back to the dotted form are the finder's, so both read alike.
[RegisterSingleton]
public sealed class LokiLogReader(LokiRunFinder loki, InspectionOptions options)
{
    // The most records one run read returns; at the limit the run comes back truncated.
    public const int Limit = 5000;

    public async Task<LogLoad> LoadAsync(RunQuery query, string sessionId, CancellationToken ct)
    {
        var logQl = $"{loki.Scope(query)} | {LokiRunFinder.Label(options.Fields.Session)}={QueryText.LogQlString(sessionId)}";
        var logs = await loki.QueryAsync(logQl, query, Limit, ct);
        var kept = logs.Where(l => l.Attributes.GetValueOrDefault(options.Fields.Project) == query.ProjectKey).OrderBy(l => l.Time).ToList();
        return new LogLoad(kept, logs.Count >= Limit);
    }
}
