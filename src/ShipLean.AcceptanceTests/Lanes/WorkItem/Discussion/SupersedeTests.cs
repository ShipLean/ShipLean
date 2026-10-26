using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Discussion;

// A teammate marking a Feature's Log entry superseded while the page is open. Its own class: the entry is added to the Feature.
[TestFixture]
public class SupersedeTests : SeededAcceptanceTest
{
    [Test]
    public async Task MarkSuperseded_WhenClicked_ShouldStrikeEntryForTeammateInRealTime()
    {
        var screen = new WorkItemScreen(Page);
        var discussion = new DiscussionPanel(Page);
        var commentId = await WorkItemComments.AddAsync(Page, SeedIds.WorkItems.Ship101, "Estimate: two sprints");
        await screen.OpenAsync("SHIP", "SHIP-101");
        await new LiveStatus(Page).WaitUntilLiveAsync();
        var entry = discussion.Comments.Filter(new LocatorFilterOptions { HasText = "Estimate: two sprints" });
        await Expect(entry).ToHaveCountAsync(1);
        await Expect(entry.GetByTestId(WorkItemTestIds.CommentSuperseded)).ToHaveCountAsync(0);

        await WorkItemComments.SupersedeAsTomAsync(Page, BaseUrl, commentId);

        await Expect(entry.GetByTestId(WorkItemTestIds.CommentSuperseded)).ToHaveCountAsync(1);
        await Expect(entry.GetByTestId(WorkItemTestIds.CommentBody)).ToHaveCSSAsync("text-decoration-line", "line-through");
    }
}
