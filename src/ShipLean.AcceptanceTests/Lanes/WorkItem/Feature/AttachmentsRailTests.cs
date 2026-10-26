using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.WorkItem;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

[TestFixture]
public class AttachmentsRailTests : SeededAcceptanceTest
{
    [Test]
    public async Task Drop_WhenFilesDropped_ShouldUploadAndGroupThem()
    {
        var rail = new AttachmentsRailPanel(Page);
        await new WorkItemScreen(Page).OpenAsync("SHIP", "SHIP-101");

        await rail.DropAsync(("drop-notes.md", "text/markdown"), ("drop-shot.png", "image/png"), ("drop-data.csv", "text/csv"));
        await Expect(rail.DropOutcomes).ToHaveCountAsync(3);
        await rail.DropUpload.ClickAsync();

        await Expect(rail.DropOverlay).ToHaveCountAsync(0);
        await Expect(rail.Markdown).ToContainTextAsync("drop-notes.md");
        await Expect(rail.Images).ToContainTextAsync("drop-shot.png");
        await Expect(rail.Files).ToContainTextAsync("drop-data.csv");
    }
}
