using ClickHouse.Client;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

// The runs linked to one item, read from the ShipLean-owned `inspection.runs` and `inspection.run_items` (filled by the views over
// the exporter's tables). The project filter and the time window are in every query; every value is a parameter.
public sealed class ClickHouseRunFinder
{
    private readonly ClickHouseDb db;
    private readonly ClickHouseSchemaMigrator migrator;
    private readonly ClickHouseNames names;

    public ClickHouseRunFinder(InspectionOptions options, ClickHouseSchemaMigrator migrator)
    {
        db = ClickHouseDb.From(options);
        this.migrator = migrator;
        names = migrator.Names;
    }

    public async Task<RunSearch> FindAsync(RunQuery query, CancellationToken ct)
    {
        try
        {
            if (!await migrator.EnsureAsync(ct))
            {
                return new RunSearch([], false, $"The exporter's tables {names.ExporterDatabase}.{names.TracesTable} and {names.ExporterDatabase}.{names.LogsTable} do not exist yet. Start the collector, which creates them.");
            }
            var runs = await ReadRunsAsync(query, ct);
            return new RunSearch(await WithLinksAsync(query, runs, ct), true, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or ClickHouseServerException or ClickHouseLayoutException or TimeoutException)
        {
            return new RunSearch([], false, ex.Message);
        }
    }

    private async Task<List<RunSummary>> ReadRunsAsync(RunQuery query, CancellationToken ct)
    {
        var rows = await db.QueryAsync($$"""
            SELECT session_id, max(agent), max(started_by), groupUniqArrayArray(models), min(started_at), max(ended_at),
                sum(span_count), sum(log_count), sum(error_count),
                sum(span_tokens_in), sum(span_tokens_in_n), sum(log_tokens_in), sum(log_tokens_in_n),
                sum(span_tokens_out), sum(span_tokens_out_n), sum(log_tokens_out), sum(log_tokens_out_n)
            FROM {{names.Runs}}
            WHERE project = {project:String}
                AND session_id IN (SELECT session_id FROM {{names.RunItems}} WHERE project = {project:String} AND item_key = {item:String})
            GROUP BY session_id
            HAVING min(started_at) <= {to:DateTime64(6, 'UTC')} AND max(ended_at) >= {from:DateTime64(6, 'UTC')}
            ORDER BY min(started_at) DESC, session_id
            """,
            new Dictionary<string, object>
            {
                ["project"] = query.ProjectKey,
                ["item"] = query.ItemKey,
                ["from"] = ClickHouseDb.Typed.Time(query.From),
                ["to"] = ClickHouseDb.Typed.Time(query.To),
            },
            ct);
        return [.. rows.Select(Summary)];
    }

    private async Task<IReadOnlyList<RunSummary>> WithLinksAsync(RunQuery query, List<RunSummary> runs, CancellationToken ct)
    {
        if (runs.Count == 0)
        {
            return runs;
        }
        var rows = await db.QueryAsync($$"""
            SELECT session_id, item_key, max(link_version)
            FROM {{names.RunItems}}
            WHERE project = {project:String} AND session_id IN {sessions:Array(String)}
            GROUP BY session_id, item_key
            """,
            new Dictionary<string, object> { ["project"] = query.ProjectKey, ["sessions"] = runs.Select(r => r.SessionId).ToArray() },
            ct);
        var links = rows.ToLookup(row => (string)row[0]!, row => new RunItemLink((string)row[1]!, Source(Convert.ToInt32(row[2], System.Globalization.CultureInfo.InvariantCulture))));
        return [.. runs.Select(run => run with { Links = [.. links[run.SessionId].OrderBy(link => link.ItemKey, StringComparer.Ordinal)] })];
    }

    private static LinkSource Source(int version) => version switch
    {
        ClickHouseViewSql.TaggedVersion => LinkSource.Tagged,
        ClickHouseViewSql.FileVersion => LinkSource.ShipleanFile,
        _ => LinkSource.FromToolCall,
    };

    private static RunSummary Summary(object?[] row)
    {
        var session = (string)row[0]!;
        var startedBy = (string)row[2]!;
        return new RunSummary(
            session,
            session,
            (string)row[1]!,
            startedBy.Length == 0 ? null : startedBy,
            [.. ((string[])row[3]!).Order(StringComparer.Ordinal)],
            Time(row[4]!),
            Time(row[5]!),
            Count(row[6]),
            Count(row[7]),
            Count(row[8]),
            Tokens(row[9], row[10], row[11], row[12]),
            Tokens(row[13], row[14], row[15], row[16]),
            []);
    }

    // Tokens sum the spans' when any span has them, else the logs', else none.
    private static long? Tokens(object? spanSum, object? spanCount, object? logSum, object? logCount)
    {
        if (Count(spanCount) > 0)
        {
            return Long(spanSum);
        }
        return Count(logCount) > 0 ? Long(logSum) : null;
    }

    private static long Long(object? value) => Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);

    private static int Count(object? value) => Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);

    private static DateTimeOffset Time(object value) => new(DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc));
}
