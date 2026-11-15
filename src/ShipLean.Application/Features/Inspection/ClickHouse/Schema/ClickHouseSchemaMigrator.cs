using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShipLean.Application.Features.Inspection.ClickHouse.Schema;

// Creates and migrates only the ShipLean `inspection` database over the exporter's tables (never the exporter's own database):
// the `runs` and `run_items` tables, four materialized views that fill them as the collector inserts, and a one-off, time-bounded
// backfill of rows written before the views existed. The backfill is recorded in `inspection.migrations` under the views'
// fingerprint, so a second start does nothing, and a changed definition (other field names) rebuilds the tables and backfills again.
// When the exporter has not created its tables yet (the collector starts later), start does nothing and the next use tries again.
public sealed class ClickHouseSchemaMigrator : IHostedService, IDisposable
{
    private readonly ClickHouseDb db;
    private readonly ClickHouseNames names;
    private readonly ClickHouseViewSql sql;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ILogger<ClickHouseSchemaMigrator>? logger;
    private readonly CancellationTokenSource stopping = new();
    private volatile string? migratedFingerprint;
    private Task startup = Task.CompletedTask;

    // The database name is a parameter so the schema tests can migrate a scratch database next to the real one.
    public ClickHouseSchemaMigrator(InspectionOptions options, string? inspectionDatabase = null, ILogger<ClickHouseSchemaMigrator>? logger = null)
    {
        db = ClickHouseDb.From(options);
        names = new ClickHouseNames(options, inspectionDatabase ?? ClickHouseNames.InspectionDatabase);
        sql = new ClickHouseViewSql(names);
        this.logger = logger;
    }

    public ClickHouseNames Names => names;

    public void Dispose()
    {
        stopping.Dispose();
        gate.Dispose();
    }

    // Start does not wait for the migration (a backfill can take a long while, and a store that does not answer must not hold the app
    // back): it begins in the background and logs how it ended. Every read of the store calls EnsureAsync, so a failed or unfinished
    // migration is retried on first use.
    public Task StartAsync(CancellationToken cancellationToken)
    {
        startup = Task.Run(MigrateInBackgroundAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await stopping.CancelAsync();
        try
        {
            await startup;
        }
        catch (OperationCanceledException)
        {
            // Stopping: the migration is retried on the next start.
        }
    }

    private async Task MigrateInBackgroundAsync()
    {
        try
        {
            if (!await EnsureAsync(stopping.Token))
            {
                logger?.LogInformation("Inspection: the exporter's tables do not exist yet; the schema is created on first use.");
            }
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            // Stopping.
        }
        catch (Exception ex) when (ClickHouseDb.IsStoreDown(ex, stopping.Token) || ex is ClickHouseLayoutException)
        {
            logger?.LogWarning(ex, "Inspection: the ClickHouse schema was not migrated at start ({Reason}); it is retried on first use.", ex.Message);
        }
    }

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
            await db.ExecuteAsync(sql.ModifyMigrationsTime, null, ct);
            await db.ExecuteAsync(sql.CreateRuns, null, ct);
            await db.ExecuteAsync(sql.CreateRunItems, null, ct);
            await db.ExecuteAsync(sql.ModifyRunsTtl, null, ct);
            await db.ExecuteAsync(sql.ModifyItemsTtl, null, ct);
            if (!await IsCurrentAsync(ct))
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

    // Views first, so rows arriving during the backfill are not missed; the backfill covers rows up to the newest one there was just after the views
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
        foreach (var view in sql.Views)
        {
            await db.ExecuteAsync(view.Create, null, ct);
        }
        // Taken after the views exist: a row landing between the two is counted twice rather than lost.
        var to = await db.ScalarAsync<long>(
            $"SELECT toUnixTimestamp64Nano(greatest((SELECT max(Timestamp) FROM {names.Traces}), (SELECT max(Timestamp) FROM {names.Logs})))", null, ct);
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

    // The newest marker says which definition is live: an older marker of the same fingerprint (a deploy, then a rollback) does not.
    private async Task<bool> IsCurrentAsync(CancellationToken ct)
    {
        var rows = await db.QueryAsync($"SELECT name FROM {names.Migrations} ORDER BY applied_at DESC, name LIMIT 1", null, ct);
        return rows.Count > 0 && (string?)rows[0][0] == MarkerName;
    }

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
