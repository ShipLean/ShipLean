using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Discussion;

[TestFixture]
public class WorkItemCommentAddedTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenCommentAdded_ShouldSaveAtOnce()
    {
        var screen = new WorkItemScreen(Page);
        var discussion = new DiscussionPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        var revision = (await WorkItemWrites.GetAsync(Page, "SHIP-147")).GetProperty("revision").GetInt32();
        var before = (await WorkItemComments.ListAsync(Page, SeedIds.WorkItems.Ship147)).GetArrayLength();
        await screen.Points.FillAsync("8");

        var response = await discussion.PostAsync(SeedIds.WorkItems.Ship147, "Checked with the vault team, **go**");

        response.Ok.ShouldBeTrue();
        await Expect(discussion.Comments).ToHaveCountAsync(before + 1);
        await Expect(discussion.Comments.Last).ToContainTextAsync("Checked with the vault team, go");
        await Expect(discussion.Input).ToHaveValueAsync(string.Empty);
        // The comment saved by itself: the open edit is still waiting for Save, and the item has no new revision.
        await Expect(screen.SaveState).ToHaveTextAsync("Unsaved: Story points");
        (await WorkItemWrites.GetAsync(Page, "SHIP-147")).GetProperty("revision").GetInt32().ShouldBe(revision);
        var stored = await WorkItemComments.ListAsync(Page, SeedIds.WorkItems.Ship147);
        stored.GetArrayLength().ShouldBe(before + 1);
        await Page.ReloadAsync();
        await screen.Screen.WaitForAsync();
        await Expect(discussion.Comments).ToHaveCountAsync(before + 1);
    }
}
