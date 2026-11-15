using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;
using ShipLean.AcceptanceTests.Pages.Backlog.Touch;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// History on a phone and the full-screen item modal against the real server (SHIP-714, L714d). The touch rules are
// checked on this task's own regions only: the History section and the modal's header.
[TestFixture]
public class ItemSideTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task History_WhenMoreThanThreeRevisionsOnPhone_ShouldShowThreeThenShowAll()
    {
        // SHIP-147 starts at revision 1; three saves behind the page's back make four revisions.
        var screen = new WorkItemScreen(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        var side = new SideRailPanel(Page);
        await Expect(side.Revisions).ToHaveCountAsync(1);
        foreach (var (revision, points) in new[] { (1, 8), (2, 13), (3, 21) })
            await WorkItemWrites.SavePointsAndPriorityAsync(Page, SeedIds.WorkItems.Ship147, revision, points, 2);
        await Page.ReloadAsync();
        var history = Page.GetByTestId(WorkItemTestIds.History);
        await history.ScrollIntoViewIfNeededAsync();
        var showAll = history.GetByTestId(WorkItemTestIds.HistoryShowAll);

        await Expect(side.Revisions).ToHaveCountAsync(3);
        await Expect(showAll).ToHaveTextAsync("Show all 4 revisions");
        await Expect(side.Revisions.First).ToHaveAttributeAsync("data-rev", "4");
        (await TouchRules.ViolationsAsync(Page, history)).ShouldBeEmpty();

        await showAll.TapAsync();

        await Expect(side.Revisions).ToHaveCountAsync(4);
        await Expect(side.Revisions.Last).ToHaveAttributeAsync("data-rev", "1");
        await Expect(showAll).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, history)).ShouldBeEmpty();
    }

    [Test]
    public async Task Sections_WhenItemHasLinkAndAttachmentOnPhone_ShouldMeetTouchRules()
    {
        // SHIP-147 is a story. A file attached through the API and a link added to the draft give both sections a row.
        var screen = new WorkItemScreen(Page);
        var side = new SideRailPanel(Page);
        var links = new LinksPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        var upload = await side.UploadAsync(SeedIds.WorkItems.Ship147, "notes.txt", "hello"u8.ToArray(), "text/plain");
        upload.Ok.ShouldBeTrue();
        await Page.ReloadAsync();
        await Expect(side.Attachments).ToHaveCountAsync(1);
        await links.AddWorkItemLinkAsync("related", "SHIP-141");
        await Expect(links.Rows).ToHaveCountAsync(1);
        var linksSection = Page.GetByTestId(WorkItemTestIds.Links);
        var attachments = Page.GetByTestId(WorkItemTestIds.Attachments);

        await linksSection.ScrollIntoViewIfNeededAsync();
        (await TouchRules.ViolationsAsync(Page, linksSection)).ShouldBeEmpty();
        await Page.GetByTestId(WorkItemTestIds.LinkAdd).TapAsync();
        await Expect(Page.GetByTestId(WorkItemTestIds.LinkForm)).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, linksSection)).ShouldBeEmpty();

        await attachments.ScrollIntoViewIfNeededAsync();
        (await TouchRules.ViolationsAsync(Page, attachments)).ShouldBeEmpty();
    }

    [Test]
    public async Task ItemModal_WhenOpenedOnPhone_ShouldFillScreenWithCloseAndOpenFullPage()
    {
        var peek = new ItemPeekDialog(Page);
        await Page.GotoAsync($"{Links.Backlog("SHIP")}?item=SHIP-141");
        await peek.WaitOpenAsync("SHIP-141");

        var panel = (await peek.Dialog.BoundingBoxAsync())!;
        panel.X.ShouldBe(0);
        panel.Y.ShouldBe(0);
        panel.Width.ShouldBe(390);
        panel.Height.ShouldBe(844);
        var close = peek.Dialog.GetByTestId(TouchTestIds.DialogClose);
        (await close.BoundingBoxAsync())!.X.ShouldBeLessThan((await peek.OpenFullPage.BoundingBoxAsync())!.X);
        await Expect(peek.Dialog.GetByTestId(TouchTestIds.DialogPrimary).GetByTestId(ItemPeekTestIds.OpenFullPage)).ToContainTextAsync("Open full page");
        // The header's two controls are the only things this task builds in the modal.
        (await TouchRules.ViolationsAsync(Page, close)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, peek.OpenFullPage)).ShouldBeEmpty();

        await close.TapAsync();

        await peek.WaitClosedAsync();
        await Expect(Page).Not.ToHaveURLAsync(new Regex("item="));
        // At 900px and below the Stories backlog lists touch rows, not the table (SHIP-716).
        await Expect(new TouchRows(Page).List).ToBeAttachedAsync();

        await Page.GotoAsync($"{Links.Backlog("SHIP")}?item=SHIP-141");
        await peek.WaitOpenAsync("SHIP-141");
        await peek.OpenFullPage.TapAsync();

        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(Links.WorkItem("SHIP", "SHIP-141"))}$"));
        await Expect(new WorkItemScreen(Page).Screen).ToBeVisibleAsync();
    }
}
