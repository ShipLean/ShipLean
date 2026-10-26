using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;

[TestFixture]
public class WorkItemLinkSaveTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenLinkAdded_ShouldSaveWithForm()
    {
        var screen = new WorkItemScreen(Page);
        var links = new LinksPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        var before = await WorkItemWrites.GetAsync(Page, "SHIP-147");
        var revision = before.GetProperty("revision").GetInt32();
        var linkCount = before.GetProperty("links").GetArrayLength();

        await links.AddWorkItemLinkAsync("blockedBy", "SHIP-120");
        await screen.Points.FillAsync("8");

        await Expect(links.Rows).ToHaveCountAsync(linkCount + 1);
        await Expect(screen.SaveState).ToContainTextAsync("Links");
        (await WorkItemWrites.GetAsync(Page, "SHIP-147")).GetProperty("links").GetArrayLength().ShouldBe(linkCount);
        var saved = await screen.SaveAsync(SeedIds.WorkItems.Ship147);

        saved.Ok.ShouldBeTrue();
        await Expect(screen.SaveState).ToHaveTextAsync($"Saved · revision {revision + 1}");
        var item = await WorkItemWrites.GetAsync(Page, "SHIP-147");
        item.GetProperty("revision").GetInt32().ShouldBe(revision + 1);
        item.GetProperty("points").GetInt32().ShouldBe(8);
        var added = item.GetProperty("links").EnumerateArray().Single(l => l.GetProperty("key").GetString() == "SHIP-120");
        added.GetProperty("type").GetString().ShouldBe("blockedBy");
        await Expect(links.Rows).ToHaveCountAsync(linkCount + 1);
        await Expect(links.Rows.Filter(new() { HasText = "unsaved" })).ToHaveCountAsync(0);
    }
}
