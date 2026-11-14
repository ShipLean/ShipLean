using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// Tasks and Discussion on a phone against the real server (SHIP-714, L714c). The touch rules are checked on this task's
// two sections only; the header, fields and rail are other tasks'.
[TestFixture]
public class ItemDiscussionTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Comment_WhenPostedWithMentionOnPhone_ShouldListMentionAboveBoxAndPost()
    {
        var screen = new WorkItemScreen(Page);
        var discussion = new DiscussionPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        var section = Page.GetByTestId(WorkItemTestIds.Discussion);
        await discussion.Input.ScrollIntoViewIfNeededAsync();
        (await TouchRules.ViolationsAsync(Page, section)).ShouldBeEmpty();

        await discussion.Input.TapAsync();
        await discussion.Input.PressSequentiallyAsync("Can you check, @pri");

        await Expect(discussion.MentionOptions).ToHaveCountAsync(1);
        var list = (await discussion.MentionMenu.BoundingBoxAsync())!;
        var box = (await discussion.Input.BoundingBoxAsync())!;
        (list.Y + list.Height).ShouldBeLessThanOrEqualTo(box.Y);
        (await TouchRules.ViolationsAsync(Page, section)).ShouldBeEmpty();
        await discussion.MentionOptions.First.TapAsync();
        await Expect(discussion.MentionMenu).ToHaveCountAsync(0);

        var posted = await Page.RunAndWaitForApiAsync(
            $"/api/work-items/{SeedIds.WorkItems.Ship147}/comments", "POST", () => discussion.Post.TapAsync());

        posted.Ok.ShouldBeTrue();
        await Expect(discussion.Comments.Last).ToContainTextAsync("@Priya");
        await Expect(discussion.Input).ToHaveValueAsync("");
        (await TouchRules.ViolationsAsync(Page, section)).ShouldBeEmpty();
    }

    [Test]
    public async Task Task_WhenStateChangedOnPhone_ShouldSaveFromNativeSelect()
    {
        var screen = new WorkItemScreen(Page);
        await screen.OpenAsync("SHIP", "SHIP-141");
        var tasks = Page.GetByTestId(WorkItemTestIds.Tasks);
        var row = Page.GetByTestId(WorkItemTestIds.TaskRow)
            .Filter(new LocatorFilterOptions { Has = Page.GetByTestId(WorkItemTestIds.TaskKey).Filter(new LocatorFilterOptions { HasTextString = "SHIP-172" }) });
        await Expect(row).ToHaveCountAsync(1);
        await row.ScrollIntoViewIfNeededAsync();
        (await TouchRules.ViolationsAsync(Page, tasks)).ShouldBeEmpty();
        var select = row.GetByTestId(WorkItemTestIds.TaskState);
        (await select.EvaluateAsync<string>("el => el.tagName")).ShouldBe("SELECT");

        var saved = await Page.RunAndWaitForApiAsync(
            $"/api/work-items/{SeedIds.WorkItems.Ship172}", "PATCH", () => select.SelectOptionAsync(new SelectOptionValue { Label = "In review" }));

        saved.Ok.ShouldBeTrue(await saved.TextAsync());
        var item = await WorkItemWrites.GetAsync(Page, "SHIP-172");
        item.GetProperty("state").GetProperty("name").GetString().ShouldBe("In review");
        (await select.EvaluateAsync<string>("el => el.selectedOptions[0].textContent")).ShouldBe("In review");
    }
}
