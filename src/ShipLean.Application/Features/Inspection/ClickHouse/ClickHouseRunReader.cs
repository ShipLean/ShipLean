using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

// One run's spans and logs, read from the exporter's tables by project and session inside the window. The run is served only when
// the finder links it to the asked item (the same check and window FindRunsAsync uses). Spans are read by session first, then every
// span of those traces; a run over MaxSpans or MaxLogs is cut there and marked Truncated. A run whose rows are gone (the exporter's
// TTL) is null, and so is a store that does not answer: the port never throws.
public sealed class ClickHouseRunReader
{
    public const int MaxSpans = 5_000;
    public const int MaxLogs = 10_000;

    private readonly ClickHouseDb db;
    private readonly ClickHouseNames names;
    private readonly ClickHouseRunFinder finder;
    private readonly InspectionFieldNames fields;

    public ClickHouseRunReader(InspectionOptions options, ClickHouseSchemaMigrator migrator, ClickHouseRunFinder finder)
    {
        db = ClickHouseDb.From(options);
        this.finder = finder;
        names = migrator.Names;
        fields = options.Fields;
    }

    public async Task<RawRun?> GetAsync(RunRef run, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(run.RunId))
        {
            return null;
        }
        try
        {
            var summary = await finder.FindOneAsync(run, ct);
            if (summary is null)
            {
                return null;
            }
            var spans = await ReadSpansAsync(run, ct);
            var logs = await ReadLogsAsync(run, ct);
            if (spans.Count == 0 && logs.Count == 0)
            {
                return null;
            }
            var truncated = spans.Count > MaxSpans || logs.Count > MaxLogs;
            return new RawRun(summary, [.. spans.Take(MaxSpans)], [.. logs.Take(MaxLogs)], truncated);
        }
        catch (Exception ex) when (ClickHouseDb.IsStoreDown(ex, ct) || ex is ClickHouseLayoutException)
        {
            return null;
        }
    }

    private async Task<List<SpanRecord>> ReadSpansAsync(RunRef run, CancellationToken ct)
    {
        var rows = await db.QueryAsync($$"""
            SELECT {{ClickHouseRowMapping.SpanSelect}}
            FROM {{names.Traces}}
            WHERE Timestamp >= {from:DateTime64(6, 'UTC')} AND Timestamp <= {to:DateTime64(6, 'UTC')}
                AND mapUpdate(ResourceAttributes, SpanAttributes)[{{names.ProjectField}}] = {project:String}
                AND TraceId IN (
                    SELECT DISTINCT TraceId FROM {{names.Traces}}
                    WHERE Timestamp >= {from:DateTime64(6, 'UTC')} AND Timestamp <= {to:DateTime64(6, 'UTC')}
                        AND mapUpdate(ResourceAttributes, SpanAttributes)[{{names.ProjectField}}] = {project:String}
                        AND mapUpdate(ResourceAttributes, SpanAttributes)[{{names.SessionField}}] = {session:String})
            ORDER BY Timestamp, SpanId
            LIMIT {limit:UInt32}
            """,
            Parameters(run, MaxSpans + 1),
            ct);
        return [.. rows.Select(ClickHouseRowMapping.Span)];
    }

    private async Task<List<LogRecordData>> ReadLogsAsync(RunRef run, CancellationToken ct)
    {
        var rows = await db.QueryAsync($$"""
            SELECT {{ClickHouseRowMapping.LogSelect}}
            FROM {{names.Logs}}
            WHERE Timestamp >= {from:DateTime64(6, 'UTC')} AND Timestamp <= {to:DateTime64(6, 'UTC')}
                AND mapUpdate(ResourceAttributes, LogAttributes)[{{names.ProjectField}}] = {project:String}
                AND mapUpdate(ResourceAttributes, LogAttributes)[{{names.SessionField}}] = {session:String}
            ORDER BY Timestamp, Body, SpanId, cityHash64(LogAttributes)
            LIMIT {limit:UInt32}
            """,
            Parameters(run, MaxLogs + 1),
            ct);
        return [.. rows.Select(row => ClickHouseRowMapping.Log(row, fields))];
    }

    private static Dictionary<string, object> Parameters(RunRef run, int limit) => new()
    {
        ["project"] = run.ProjectKey,
        ["session"] = run.RunId,
        ["from"] = ClickHouseDb.Typed.Time(run.From),
        ["to"] = ClickHouseDb.Typed.Time(run.To),
        ["limit"] = (uint)limit,
    };
}
