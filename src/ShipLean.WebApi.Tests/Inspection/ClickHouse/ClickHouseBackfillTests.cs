using ShipLean.Application.Features.Inspection.ClickHouse.Schema;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse;

// Rows the exporter wrote before the views existed are counted once when Inspection starts, and starting again changes nothing.
[TestFixture(TestName = "ClickHouse backfill, SHIP-1556")]
[Category("SHIP-1556")]
public sealed class ClickHouseBackfillTests
{
    [Test]
    public async Task Start_WhenRowsExistBeforeTheViews_CountsThemOnceAndASecondStartDoesNotRepeatIt()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        var tag = new Dictionary<string, string> { ["shiplean.item"] = "SHIP-1" };
        await scratch.AddSpanAsync("s1", 0, 10, "Unset", tag);
        await scratch.AddSpanAsync("s1", 20, 5, "Error", tag);
        // Stamped far ahead of the clock: the backfill goes up to the newest row, not up to now.
        await scratch.AddSpanAsync("s1", 4 * 3600, 1, "Unset", tag);
        await scratch.AddLogAsync("s1", 30, "ERROR", tag);

        await scratch.Migrator().StartAsync(CancellationToken.None);
        // A sentinel only a second backfill (which rebuilds the table) would wipe.
        await scratch.Db.ExecuteAsync(
            $"INSERT INTO `{scratch.InspectionDatabase}`.`run_items` (project, item_key, session_id, link_version, seen_at) VALUES ('SENTINEL', 'SHIP-1', 'sentinel-session', 3, now())", null, CancellationToken.None);
        var second = scratch.Migrator();
        await second.StartAsync(CancellationToken.None);

        var search = await scratch.Finder(second).FindAsync(ScratchExporter.Query("SHIP-1") with { To = ScratchExporter.Query("SHIP-1").To.AddHours(5) }, CancellationToken.None);
        search.Runs.Count.ShouldBe(1, search.Problem);
        (search.Runs[0].SpanCount, search.Runs[0].LogCount, search.Runs[0].ErrorCount).ShouldBe((3, 1, 2));
        var sentinels = await scratch.Db.ScalarAsync<long>($"SELECT count() FROM `{scratch.InspectionDatabase}`.`run_items` WHERE project = 'SENTINEL'", null, CancellationToken.None);
        sentinels.ShouldBe(1, "the second start repeated the backfill");
    }

    [Test]
    public async Task Start_WhenRowsArriveAfterTheViews_CountsThemToo()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        var migrator = scratch.Migrator();
        await migrator.StartAsync(CancellationToken.None);

        await scratch.AddSpanAsync("s2", 0, 10, "Unset", new Dictionary<string, string> { ["shiplean.item"] = "SHIP-2" });

        var search = await scratch.Finder(migrator).FindAsync(ScratchExporter.Query("SHIP-2"), CancellationToken.None);
        search.Runs.Count.ShouldBe(1, search.Problem);
        search.Runs[0].SpanCount.ShouldBe(1);
    }
}
