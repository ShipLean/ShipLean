using System.Globalization;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

// Tokens per task from the ShipLean-owned `inspection.runs` and `inspection.run_items` (SHIP-1398 CK1), in one query per call. A run
// counts toward the items at its strongest link seen in the window, its tokens split evenly over them (item D21). The project filter
// is in both subqueries, the window is cut to the second as the link stamp is, and every value is a parameter.
public sealed class ClickHouseTaskTokenTotals : ITaskTokenTotals
{
    private readonly ClickHouseDb db;
    private readonly ClickHouseSchemaMigrator migrator;
    private readonly ClickHouseNames names;
    private readonly InspectionOptions options;
    private readonly TimeProvider time;

    public ClickHouseTaskTokenTotals(InspectionOptions options, ClickHouseSchemaMigrator migrator, TimeProvider? time = null)
    {
        this.options = options;
        this.migrator = migrator;
        this.time = time ?? TimeProvider.System;
        db = ClickHouseDb.From(options);
        names = migrator.Names;
    }

    public async Task<TaskTokenTotals> ReadAsync(TaskTokenQuery query, CancellationToken ct)
    {
        try
        {
            if (!await migrator.EnsureAsync(ct))
            {
                return TaskTokenTotals.Unavailable($"The exporter's tables {names.ExporterDatabase}.{names.TracesTable} and {names.ExporterDatabase}.{names.LogsTable} do not exist yet. Start the collector, which creates them.");
            }
            var rows = await db.QueryAsync($$"""
                SELECT l.item_key, l.n, l.rn, r.models, r.tokens_in, r.tokens_out
                FROM (
                    SELECT session_id, item_key, n, rn
                    FROM (
                        SELECT session_id, item_key, v,
                            max(v) OVER (PARTITION BY session_id) AS top,
                            count() OVER (PARTITION BY session_id, v) AS n,
                            row_number() OVER (PARTITION BY session_id, v ORDER BY item_key) AS rn
                        FROM (
                            SELECT session_id, item_key, max(link_version) AS v
                            FROM {{names.RunItems}}
                            WHERE project = {project:String} AND {{LinkSeenInWindow}}
                            GROUP BY session_id, item_key
                        )
                    )
                    WHERE v = top AND item_key IN {keys:Array(String)}
                ) AS l
                INNER JOIN (
                    SELECT session_id, groupUniqArrayArray(models) AS models,
                        if(sum(span_tokens_in_n) > 0, sum(span_tokens_in), if(sum(log_tokens_in_n) > 0, sum(log_tokens_in), 0)) AS tokens_in,
                        if(sum(span_tokens_out_n) > 0, sum(span_tokens_out), if(sum(log_tokens_out_n) > 0, sum(log_tokens_out), 0)) AS tokens_out
                    FROM {{names.Runs}}
                    WHERE project = {project:String}
                    GROUP BY session_id
                ) AS r ON l.session_id = r.session_id
                """,
                new Dictionary<string, object>
                {
                    ["project"] = query.ProjectKey,
                    ["keys"] = query.ItemKeys.ToArray(),
                    ["from"] = ClickHouseDb.Typed.Time(query.From),
                    ["to"] = ClickHouseDb.Typed.Time(query.To),
                },
                ct);
            return new TaskTokenTotals(true, null, time.GetUtcNow().AddDays(-(options.RetentionDays + ClickHouseViewSql.ExpiredKeepDays)), Totals(rows));
        }
        catch (Exception ex) when (ClickHouseDb.IsStoreDown(ex, ct) || ex is ClickHouseLayoutException)
        {
            return TaskTokenTotals.Unavailable(ex.Message);
        }
    }

    // One (run, item) row each. The integer share goes to every item; the remainder to the run's first item in ordinal order (rn 1).
    private static Dictionary<string, TaskTokens> Totals(IReadOnlyList<object?[]> rows)
    {
        var totals = new Dictionary<string, TaskTokens>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var key = (string)row[0]!;
            var n = Long(row[1]);
            var first = Long(row[2]) == 1;
            var tokensIn = Long(row[4]);
            var tokensOut = Long(row[5]);
            var share = new TaskTokens(1, (tokensIn / n) + (first ? tokensIn % n : 0), (tokensOut / n) + (first ? tokensOut % n : 0), [.. (string[])row[3]!]);
            totals[key] = totals.TryGetValue(key, out var have) ? Add(have, share) : share;
        }
        return totals;
    }

    private static TaskTokens Add(TaskTokens a, TaskTokens b)
        => new(a.Runs + b.Runs, a.TokensIn + b.TokensIn, a.TokensOut + b.TokensOut, [.. a.Models.Union(b.Models, StringComparer.Ordinal).Order(StringComparer.Ordinal)]);

    private static long Long(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);

    // The same stamp test as ClickHouseRunFinder: both ends cut to the second.
    private const string LinkSeenInWindow = "seen_at BETWEEN toDateTime({from:DateTime64(6, 'UTC')}, 'UTC') AND toDateTime({to:DateTime64(6, 'UTC')}, 'UTC')";
}
