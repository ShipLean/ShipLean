using System.Diagnostics;
using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

// Is the store reachable, how long does a round trip take and how far behind the exporter's tables are the views.
// ShipLean's ClickHouse user should only read them (the docs recommend a read-only user); health does not probe for write grants.
public sealed class ClickHouseHealth
{
    private readonly ClickHouseDb db;
    private readonly ClickHouseSchemaMigrator migrator;
    private readonly ClickHouseNames names;

    public ClickHouseHealth(InspectionOptions options, ClickHouseSchemaMigrator migrator)
    {
        db = ClickHouseDb.From(options);
        this.migrator = migrator;
        names = migrator.Names;
    }

    public async Task<StoreHealth> CheckAsync(CancellationToken ct)
    {
        try
        {
            var watch = Stopwatch.StartNew();
            await db.ScalarAsync<long>("SELECT 1", null, ct);
            var latency = (int)watch.ElapsedMilliseconds;
            if (!await migrator.EnsureAsync(ct))
            {
                return new StoreHealth(true, latency, null, $"The exporter's tables {names.ExporterDatabase}.{names.TracesTable} and {names.ExporterDatabase}.{names.LogsTable} do not exist yet. Start the collector, which creates them.");
            }
            return new StoreHealth(true, latency, await ViewLagAsync(ct), null);
        }
        catch (Exception ex) when (ClickHouseDb.IsStoreDown(ex, ct) || ClickHouseDb.IsSchemaError(ex) || ex is ClickHouseLayoutException)
        {
            return new StoreHealth(false, null, null, ex.Message);
        }
    }

    // How far the newest record the views have seen trails the newest record in the exporter's tables that the views take (those with a
    // project and a session), in the last day; null with no data.
    private async Task<int?> ViewLagAsync(CancellationToken ct)
    {
        var rows = await db.QueryAsync($$"""
            SELECT
                toInt64(toUnixTimestamp(greatest(
                    (SELECT max(toDateTime(Timestamp)) FROM {{names.Traces}}
                        WHERE Timestamp >= now() - INTERVAL 1 DAY AND mapUpdate(ResourceAttributes, SpanAttributes)[{{names.ProjectField}}] != '' AND mapUpdate(ResourceAttributes, SpanAttributes)[{{names.SessionField}}] != ''),
                    (SELECT max(toDateTime(Timestamp)) FROM {{names.Logs}}
                        WHERE Timestamp >= now() - INTERVAL 1 DAY AND mapUpdate(ResourceAttributes, LogAttributes)[{{names.ProjectField}}] != '' AND mapUpdate(ResourceAttributes, LogAttributes)[{{names.SessionField}}] != '')))) AS source,
                toInt64(toUnixTimestamp((SELECT max(toDateTime(last_seen)) FROM {{names.Runs}} WHERE last_seen >= now() - INTERVAL 1 DAY))) AS seen
            """,
            null,
            ct);
        var source = Convert.ToInt64(rows[0][0], System.Globalization.CultureInfo.InvariantCulture);
        var seen = Convert.ToInt64(rows[0][1], System.Globalization.CultureInfo.InvariantCulture);
        return source == 0 ? null : (int)Math.Max(0, source - seen);
    }
}
