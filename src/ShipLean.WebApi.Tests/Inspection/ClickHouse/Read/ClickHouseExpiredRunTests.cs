using ShipLean.Application.Features.Inspection.ClickHouse;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse.Read;

// A run whose exporter rows are gone (its TTL ran) stays listed for its item, and GetRun answers null for it: that pair is how the views tell
// an expired run from an unknown one (D31). Both tables outlive the exporter's retention by ExpiredKeepDays.
[TestFixture(TestName = "ClickHouse expired run, SHIP-1660")]
[Category("SHIP-1557")]
public sealed class ClickHouseExpiredRunTests
{
    [Test]
    public async Task Run_WhenItsExporterRowsAreGone_StaysListedAndGetRunIsNull()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await scratch.AddSpanAsync("s1", 0, 10, "Unset", new Dictionary<string, string> { ["shiplean.item"] = "SHIP-1" });
        var migrator = scratch.Migrator();
        var finder = scratch.Finder(migrator);
        var reader = new ClickHouseRunReader(scratch.Options, migrator, finder);
        var query = ScratchExporter.Query("SHIP-1");
        var run = new RunRef(query.ProjectKey, "SHIP-1", "s1", query.From, query.To, null, null);
        (await reader.GetAsync(run, CancellationToken.None)).ShouldNotBeNull();

        await scratch.Db.ExecuteAsync($"ALTER TABLE {scratch.ExporterDatabase}.otel_traces DELETE WHERE 1 SETTINGS mutations_sync = 2", null, CancellationToken.None);

        var search = await finder.FindAsync(query, CancellationToken.None);
        search.Reachable.ShouldBeTrue(search.Problem);
        search.Runs.Select(r => r.RunId).ShouldBe(["s1"]);
        (await reader.GetAsync(run, CancellationToken.None)).ShouldBeNull();
    }

    [Test]
    public async Task Tables_ShouldKeepTheirRowsForRetentionPlusTheExpiredKeepDays()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await scratch.Migrator().EnsureAsync(CancellationToken.None);

        foreach (var table in new[] { "runs", "run_items" })
        {
            var ddl = await scratch.Db.ScalarAsync<string>(
                "SELECT create_table_query FROM system.tables WHERE database = {db:String} AND name = {table:String}",
                new Dictionary<string, object> { ["db"] = scratch.InspectionDatabase, ["table"] = table }, CancellationToken.None);
            var days = scratch.Options.RetentionDays + 30;
            ddl.ShouldContain($"toIntervalDay({days})", customMessage: table);
        }
    }
}
