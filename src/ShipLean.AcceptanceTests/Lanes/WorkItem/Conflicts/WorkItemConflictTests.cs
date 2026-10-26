using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;

[TestFixture]
public class WorkItemConflictTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenStaleSave_ShouldListTheirChangesAndReapplyMine()
    {
        var screen = new WorkItemScreen(Page);
        var conflict = new ConflictPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-141");
        var before = (await WorkItemWrites.GetAsync(Page, "SHIP-141")).GetProperty("revision").GetInt32();
        await screen.Points.FillAsync("13");

        // A teammate saves between this page's load and its Save.
        await WorkItemWrites.SavePointsAndPriorityAsync(Page, SeedIds.WorkItems.Ship141, before, points: 3, priority: 1);
        var stale = await screen.SaveAsync(SeedIds.WorkItems.Ship141);

        stale.Status.ShouldBe(409);
        await Expect(conflict.Banner).ToBeVisibleAsync();
        await Expect(conflict.Summary).ToContainTextAsync("SHIP-141 first");
        await Expect(conflict.TheirChanges).ToHaveCountAsync(2);
        await Expect(conflict.TheirChanges.Nth(0)).ToContainTextAsync("Priority");
        await Expect(conflict.TheirChanges.Nth(1)).ToContainTextAsync("Story points");
        await Expect(conflict.AlsoMine).ToHaveCountAsync(1);
        await Expect(screen.Points).ToHaveValueAsync("13");

        await conflict.ReapplyMine.ClickAsync();

        await Expect(conflict.Banner).ToHaveCountAsync(0);
        await Expect(screen.Points).ToHaveValueAsync("13");
        await Expect(screen.Save).ToBeEnabledAsync();
        var saved = await screen.SaveAsync(SeedIds.WorkItems.Ship141);
        saved.Ok.ShouldBeTrue();
        await Expect(screen.SaveState).ToHaveTextAsync($"Saved · revision {before + 2}");
        var item = await WorkItemWrites.GetAsync(Page, "SHIP-141");
        item.GetProperty("points").GetInt32().ShouldBe(13);
        item.GetProperty("priority").GetInt32().ShouldBe(1);
        item.GetProperty("revision").GetInt32().ShouldBe(before + 2);
    }
}
