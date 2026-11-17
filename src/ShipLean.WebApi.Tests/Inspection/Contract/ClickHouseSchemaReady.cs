using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.LocalEnvironment.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Contract;

// The ShipLean `inspection` schema over the shared stack's ClickHouse, migrated once per test run, under one lock, before any
// fixture reads or writes it. Every fixture builds its own ClickHouseSchemaMigrator; on a fresh stack each found no marker and
// rebuilt, so one dropped the tables another was filling or reading (a cold run gave Available false, and a test's INSERT into
// run_items landed before the database existed). After this, every later migrator finds the marker current and rebuilds nothing.
public static class ClickHouseSchemaReady
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool migrated;

    // Call once the stack has shown a load: the collector has created the exporter's tables by then, so the migration must succeed.
    public static async Task EnsureAsync(InspectionStack stack, TimeSpan timeout)
    {
        await Gate.WaitAsync();
        try
        {
            if (migrated)
            {
                return;
            }
            using var migrator = new ClickHouseSchemaMigrator(stack.Options("clickhouse"));
            using var deadline = new CancellationTokenSource(timeout);
            (await migrator.EnsureAsync(deadline.Token)).ShouldBeTrue("the exporter's tables exist once a load is visible, so the schema must migrate");
            migrated = true;
        }
        finally
        {
            Gate.Release();
        }
    }
}
