using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Side;

// A teammate's attachment arriving over the hub while the page is open. Its own class: the file is added to the item.
[TestFixture]
public class WorkItemTeammateAttachmentTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenTeammateAttaches_ShouldShowItWithoutReload()
    {
        var screen = new WorkItemScreen(Page);
        var side = new SideRailPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-151");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(side.AttachmentCount).ToHaveTextAsync("0");

        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship151, "from-tom.txt", [7, 7]);

        await Expect(side.Attachments).ToHaveCountAsync(1);
        await Expect(side.Downloads).ToHaveTextAsync("from-tom.txt");
    }
}
