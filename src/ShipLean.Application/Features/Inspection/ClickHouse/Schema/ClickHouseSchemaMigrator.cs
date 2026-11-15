using Microsoft.Extensions.Hosting;

namespace ShipLean.Application.Features.Inspection.ClickHouse.Schema;

// Creates and migrates only the ShipLean `inspection` database over the exporter's tables (never the exporter's own database):
// the `runs` and `run_items` tables, four materialized views that fill them as the collector inserts, and a one-off, time-bounded
// backfill of rows written before the views existed. The backfill is recorded in `inspection.migrations` under the views'
// fingerprint, so a second start does nothing, and a changed definition (other field names) rebuilds the tables and backfills again.
// When the exporter has not created its tables yet (the collector starts later), start does nothing and the next use tries again.
public sealed class ClickHouseSchemaMigrator : IHostedService
{
    private readonly ClickHouseDb db;
    private readonly ClickHouseNames names;
    private readonly ClickHouseViewSql sql;
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile string? migratedFingerprint;

    public ClickHouseSchemaMigrator(InspectionOptions options)
        : this(options, ClickHouseNames.InspectionDatabase)
    {
    }

    // The database name is a parameter so the schema tests can migrate a scratch database next to the real one.
    public ClickHouseSchemaMigrator(InspectionOptions options, string inspectionDatabase)
    {
        db = ClickHouseDb.From(options);
        names = new ClickHouseNames(options, inspectionDatabase);
        sql = new ClickHouseViewSql(names);
    }

    public ClickHouseNames Names => names;

    public Task StartAsync(CancellationToken cancellationToken) => EnsureAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Migrates when not done yet; true when the schema is in place, false when the exporter's tables are not there yet. Throws
    // ClickHouseLayoutException when a table lacks a column the views read.
    public async Task<bool> EnsureAsync(CancellationToken ct)
    {
        if (migratedFingerprint == sql.Fingerprint)
        {
            return true;
        }
        await gate.WaitAsync(ct);
        try
        {
            if (!await ExporterTablesExistAsync(ct))
            {
                return false;
            }
            await CheckLayoutAsync(names.TracesTable, names.ExporterDatabase, ClickHouseViewSql.TraceColumns, ct);
            await CheckLayoutAsync(names.LogsTable, names.ExporterDatabase, ClickHouseViewSql.LogColumns, ct);
            await db.ExecuteAsync(sql.CreateDatabase, null, ct);
            await db.ExecuteAsync(sql.CreateMigrations, null, ct);
            await db.ExecuteAsync(sql.CreateRuns, null, ct);
            await db.ExecuteAsync(sql.CreateRunItems, null, ct);
            await db.ExecuteAsync(sql.ModifyRunsTtl, null, ct);
            await db.ExecuteAsync(sql.ModifyItemsTtl, null, ct);
            if (!await BackfillRecordedAsync(ct))
            {
                await RebuildAsync(ct);
            }
            migratedFingerprint = sql.Fingerprint;
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    // Views first, so rows arriving during the backfill are not missed; the backfill covers rows up to the newest one there was when the views
    // went in (so a row stamped ahead of the clock is still counted). Tables are dropped and recreated first: a definition change must not leave rows of the old one behind.
    private async Task RebuildAsync(CancellationToken ct)
    {
        foreach (var view in sql.Views)
        {
            await db.ExecuteAsync(view.Drop, null, ct);
        }
        await db.ExecuteAsync($"DROP TABLE IF EXISTS {names.Runs}", null, ct);
        await db.ExecuteAsync($"DROP TABLE IF EXISTS {names.RunItems}", null, ct);
        await db.ExecuteAsync(sql.CreateRuns, null, ct);
        await db.ExecuteAsync(sql.CreateRunItems, null, ct);
        var to = await db.ScalarAsync<long>(
            $"SELECT toUnixTimestamp64Nano(greatest((SELECT max(Timestamp) FROM {names.Traces}), (SELECT max(Timestamp) FROM {names.Logs})))", null, ct);
        foreach (var view in sql.Views)
        {
            await db.ExecuteAsync(view.Create, null, ct);
        }
        var parameters = new Dictionary<string, object> { ["days"] = (uint)names.RetentionDays, ["to"] = to };
        foreach (var view in sql.Views)
        {
            await db.ExecuteAsync(view.Backfill, parameters, ct);
        }
        await db.ExecuteAsync(
            $"INSERT INTO {names.Migrations} (name) VALUES ({{name:String}})",
            new Dictionary<string, object> { ["name"] = MarkerName }, ct);
    }

    private string MarkerName => "views-" + sql.Fingerprint;

    private async Task<bool> BackfillRecordedAsync(CancellationToken ct)
        => await db.ScalarAsync<long>($"SELECT count() FROM {names.Migrations} WHERE name = {{name:String}}", new Dictionary<string, object> { ["name"] = MarkerName }, ct) > 0;

    private async Task<bool> ExporterTablesExistAsync(CancellationToken ct)
    {
        var found = await db.ScalarAsync<long>(
            "SELECT count() FROM system.tables WHERE database = {db:String} AND name IN ({traces:String}, {logs:String})",
            new Dictionary<string, object> { ["db"] = names.ExporterDatabase, ["traces"] = names.TracesTable, ["logs"] = names.LogsTable }, ct);
        return found == 2;
    }

    private async Task CheckLayoutAsync(string table, string database, IReadOnlyList<string> required, CancellationToken ct)
    {
        var rows = await db.QueryAsync(
            "SELECT name FROM system.columns WHERE database = {db:String} AND table = {table:String}",
            new Dictionary<string, object> { ["db"] = database, ["table"] = table }, ct);
        var present = rows.Select(row => (string)row[0]!).ToHashSet(StringComparer.Ordinal);
        var missing = required.Where(column => !present.Contains(column)).ToList();
        if (missing.Count > 0)
        {
            throw new ClickHouseLayoutException(
                $"The exporter's table {database}.{table} has no column {string.Join(", ", missing)}, which Inspection reads. Use the OpenTelemetry collector's clickhouse exporter with create_schema on, or add the column.");
        }
    }
}
