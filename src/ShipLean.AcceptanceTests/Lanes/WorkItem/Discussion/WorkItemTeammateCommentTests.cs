using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Discussion;

// A teammate's comment arriving over the hub while the page is open. Its own class: the comment is added to the item.
[TestFixture]
public class WorkItemTeammateCommentTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenTeammateComments_ShouldShowItWithoutReload()
    {
        var screen = new WorkItemScreen(Page);
        var discussion = new DiscussionPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var before = (await WorkItemComments.ListAsync(Page, SeedIds.WorkItems.Ship147)).GetArrayLength();
        await Expect(discussion.Comments).ToHaveCountAsync(before);

        await WorkItemComments.AddAsTomAsync(Page, BaseUrl, SeedIds.WorkItems.Ship147, "Tom here, the vault is ready");

        await Expect(discussion.Comments).ToHaveCountAsync(before + 1);
        await Expect(discussion.Comments.Last).ToContainTextAsync("Tom here, the vault is ready");
    }
}
