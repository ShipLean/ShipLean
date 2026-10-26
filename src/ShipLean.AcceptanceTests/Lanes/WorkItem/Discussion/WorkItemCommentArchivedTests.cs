using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Discussion;

[TestFixture]
public class WorkItemCommentArchivedTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenCommentArchived_ShouldStayInDatabase()
    {
        var screen = new WorkItemScreen(Page);
        var discussion = new DiscussionPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        var id = await WorkItemComments.AddAsync(Page, SeedIds.WorkItems.Ship147, "Remove me");
        await Page.ReloadAsync();
        await screen.Screen.WaitForAsync();
        var shown = Page.GetByTestId(WorkItemTestIds.Comment).Filter(new() { HasText = "Remove me" });
        await Expect(shown).ToHaveCountAsync(1);

        var response = await Page.RunAndWaitForApiAsync($"/api/comments/{id}/archive", "POST",
            () => shown.GetByTestId(WorkItemTestIds.CommentArchive).ClickAsync());

        response.Ok.ShouldBeTrue();
        await Expect(shown).ToHaveCountAsync(0);
        var listed = await WorkItemComments.ListAsync(Page, SeedIds.WorkItems.Ship147);
        listed.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ShouldNotContain(id);
        var row = await WorkItemComments.RowAsync(ConnectionString, id);
        row.ShouldNotBeNull().Archived.ShouldBeTrue();
        row.Value.Body.ShouldBe("Remove me");
        await Page.ReloadAsync();
        await screen.Screen.WaitForAsync();
        await Expect(shown).ToHaveCountAsync(0);
        await Expect(discussion.Count).ToHaveTextAsync(listed.GetArrayLength().ToString());
    }
}
