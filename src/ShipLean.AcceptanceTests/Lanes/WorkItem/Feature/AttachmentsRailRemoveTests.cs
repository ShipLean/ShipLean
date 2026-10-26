using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.WorkItem;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

[TestFixture]
public class AttachmentsRailRemoveTests : SeededAcceptanceTest
{
    [Test]
    public async Task AttachmentsRail_WhenRemoved_ShouldLeaveTheList()
    {
        var rail = new AttachmentsRailPanel(Page);
        await new WorkItemScreen(Page).OpenAsync("SHIP", "SHIP-101");
        await rail.DropAsync(("remove-me.md", "text/markdown"));
        await rail.DropUpload.ClickAsync();
        await Expect(rail.Markdown).ToContainTextAsync("remove-me.md");

        await rail.RemoveOf("remove-me.md").ClickAsync();

        await Expect(rail.Items.Filter(new LocatorFilterOptions { HasText = "remove-me.md" })).ToHaveCountAsync(0);
    }
}
