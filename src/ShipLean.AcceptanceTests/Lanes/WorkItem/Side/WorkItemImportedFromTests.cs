using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Side;

// Its own class: one item is marked as imported.
[TestFixture]
public class WorkItemImportedFromTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenImported_ShouldShowWhereItCameFrom()
    {
        var screen = new WorkItemScreen(Page);
        var side = new SideRailPanel(Page);
        await WorkItemAttachments.MarkImportedAsync(ConnectionString, SeedIds.WorkItems.Ship153, "PLATFORM-412", "https://jira.example.com/browse/PLATFORM-412");

        await screen.OpenAsync("SHIP", "SHIP-153");

        await Expect(side.ImportedFrom).ToContainTextAsync("Imported from Jira");
        await Expect(side.ImportedFromLink).ToHaveTextAsync("PLATFORM-412 ↗");
        await Expect(side.ImportedFromLink).ToHaveAttributeAsync("href", "https://jira.example.com/browse/PLATFORM-412");
    }

    [Test]
    public async Task WorkItem_WhenNotImported_ShouldNotShowImportedFrom()
    {
        var screen = new WorkItemScreen(Page);
        var side = new SideRailPanel(Page);

        await screen.OpenAsync("SHIP", "SHIP-147");

        await Expect(side.Revisions.First).ToBeVisibleAsync();
        await Expect(side.ImportedFrom).ToHaveCountAsync(0);
    }
}
