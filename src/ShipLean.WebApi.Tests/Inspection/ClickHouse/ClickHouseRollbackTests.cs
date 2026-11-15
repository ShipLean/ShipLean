using ShipLean.Application.Features.Inspection.ClickHouse.Schema;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.ClickHouse;

// A deploy that changes the views and a rollback that changes them back must leave the rolled-back definition live, with its data.
[TestFixture(TestName = "ClickHouse views rollback, SHIP-1556")]
[Category("SHIP-1556")]
public sealed class ClickHouseRollbackTests
{
    [Test]
    public async Task Ensure_WhenTheViewsChangeAndChangeBack_RebuildsThemEachTime()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await scratch.AddSpanAsync("s1", 0, 1, "Unset", new Dictionary<string, string> { ["shiplean.item"] = "SHIP-1" });
        var other = scratch.Options with { Fields = scratch.Options.Fields with { Item = "shiplean.other_item" } };
        var query = ScratchExporter.Query("SHIP-1");

        var first = scratch.Migrator();
        await first.EnsureAsync(CancellationToken.None);
        var before = await scratch.Finder(first).FindAsync(query, CancellationToken.None);
        var changed = scratch.Migrator(other);
        await changed.EnsureAsync(CancellationToken.None);
        var during = await scratch.Finder(changed).FindAsync(query, CancellationToken.None);
        var rolledBack = scratch.Migrator();
        await rolledBack.EnsureAsync(CancellationToken.None);
        var after = await scratch.Finder(rolledBack).FindAsync(query, CancellationToken.None);

        before.Runs.Count.ShouldBe(1, before.Problem);
        during.Runs.Count.ShouldBe(0, "the changed views read another attribute, so the item is not linked");
        after.Runs.Count.ShouldBe(1, after.Problem);
        after.Runs[0].SpanCount.ShouldBe(1, "the rolled-back views were backfilled once, not twice");
    }
}
