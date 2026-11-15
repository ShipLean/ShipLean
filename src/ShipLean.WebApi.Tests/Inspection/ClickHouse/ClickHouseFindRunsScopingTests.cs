using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse;

// The finder alone: its project filter, its time window and its order, each with a positive control.
[TestFixture(TestName = "ClickHouse FindRuns scoping, SHIP-1556")]
[Category("SHIP-1556")]
public sealed class ClickHouseFindRunsScopingTests
{
    private static readonly Dictionary<string, string> Tag = new() { ["shiplean.item"] = "SHIP-1" };

    [Test]
    public async Task FindRuns_WhenAskedForAnotherProject_FindsNothing()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await scratch.AddSpanAsync("s1", 0, 10, "Unset", Tag);
        var migrator = scratch.Migrator();
        var finder = scratch.Finder(migrator);

        var own = await finder.FindAsync(ScratchExporter.Query("SHIP-1"), CancellationToken.None);
        var other = await finder.FindAsync(ScratchExporter.Query("SHIP-1") with { ProjectKey = "OTHER" }, CancellationToken.None);

        own.Runs.Count.ShouldBe(1, own.Problem);
        other.Reachable.ShouldBeTrue(other.Problem);
        other.Runs.ShouldBeEmpty();
    }

    [Test]
    public async Task FindRuns_WhenTheWindowIsBeforeOrAfterTheRun_FindsNothing()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await scratch.AddSpanAsync("s1", 0, 10, "Unset", Tag);
        var finder = scratch.Finder(scratch.Migrator());
        var query = ScratchExporter.Query("SHIP-1");

        var inside = await finder.FindAsync(query, CancellationToken.None);
        var before = await finder.FindAsync(query with { From = query.From.AddHours(-5), To = query.From.AddHours(-4) }, CancellationToken.None);
        var after = await finder.FindAsync(query with { From = query.To.AddHours(1), To = query.To.AddHours(2) }, CancellationToken.None);

        inside.Runs.Count.ShouldBe(1, inside.Problem);
        before.Runs.ShouldBeEmpty();
        after.Runs.ShouldBeEmpty();
    }

    [Test]
    public async Task FindRuns_WhenSeveralRunsMatch_ReturnsTheNewestFirst()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await scratch.AddSpanAsync("middle", 600, 10, "Unset", Tag);
        await scratch.AddSpanAsync("oldest", 0, 10, "Unset", Tag);
        await scratch.AddSpanAsync("newest", 1200, 10, "Unset", Tag);
        var finder = scratch.Finder(scratch.Migrator());

        var search = await finder.FindAsync(ScratchExporter.Query("SHIP-1"), CancellationToken.None);

        search.Runs.Select(run => run.SessionId).ToList().ShouldBe(["newest", "middle", "oldest"], search.Problem);
    }
}
