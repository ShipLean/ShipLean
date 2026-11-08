using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Conflicts;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.AcceptanceTests.Pages.WorkItem.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// The work item page's frame on a phone against the real server (SHIP-714, L714a): the Save bar names what changed and
// saves one revision, and a stale save brings the conflict banner into view. The touch rules are checked on this task's
// regions only (the section chips, the Save bar and the conflict banner); the fields, tasks and rail are other tasks'.
[TestFixture]
public class ItemFrameTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Item_WhenTitleEditedOnPhone_ShouldShowSaveBarAndSaveOneRevision()
    {
        var screen = new WorkItemScreen(Page);
        var frame = new ItemFrameTouch(Page);
        var touch = new TouchShell(Page);
        await screen.OpenAsync("SHIP", "SHIP-144");
        var before = (await WorkItemWrites.GetAsync(Page, "SHIP-144")).GetProperty("revision").GetInt32();
        await Expect(frame.SectionChips).ToBeVisibleAsync();
        await Expect(touch.SaveBar).ToHaveCountAsync(0);

        await screen.Title.FillAsync("Phone-edited title");

        await Expect(touch.SaveBarMessage).ToHaveTextAsync("Title");
        (await TouchRules.ViolationsAsync(Page, frame.SectionChips)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, touch.SaveBar)).ShouldBeEmpty();
        var saved = await Page.RunAndWaitForApiAsync(
            $"/api/work-items/{SeedIds.WorkItems.Ship144}", "PATCH", () => touch.SaveBarSave.TapAsync());

        saved.Ok.ShouldBeTrue();
        await Expect(touch.SaveBar).ToHaveCountAsync(0);
        await Expect(screen.Title).ToHaveValueAsync("Phone-edited title");
        var item = await WorkItemWrites.GetAsync(Page, "SHIP-144");
        item.GetProperty("title").GetString().ShouldBe("Phone-edited title");
        item.GetProperty("revision").GetInt32().ShouldBe(before + 1);
    }

    [Test]
    public async Task Item_WhenSaveConflictsOnPhone_ShouldScrollConflictBannerIntoView()
    {
        var screen = new WorkItemScreen(Page);
        var frame = new ItemFrameTouch(Page);
        var touch = new TouchShell(Page);
        var conflict = new ConflictPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-141");
        var before = (await WorkItemWrites.GetAsync(Page, "SHIP-141")).GetProperty("revision").GetInt32();
        await screen.Points.FillAsync("13");
        // Read on down the page, so the banner (under the fields) starts out of view.
        await frame.SectionChip("History").TapAsync();
        await Expect(screen.Points).Not.ToBeInViewportAsync();

        // A teammate saves between this page's load and its Save.
        await WorkItemWrites.SavePointsAndPriorityAsync(Page, SeedIds.WorkItems.Ship141, before, points: 3, priority: 1);
        var stale = await Page.RunAndWaitForApiAsync(
            $"/api/work-items/{SeedIds.WorkItems.Ship141}", "PATCH", () => touch.SaveBarSave.TapAsync());

        stale.Status.ShouldBe(409);
        await Expect(conflict.Banner).ToBeInViewportAsync();
        var banner = (await conflict.Banner.BoundingBoxAsync())!;
        var reload = (await conflict.ReloadTheirs.BoundingBoxAsync())!;
        var reapply = (await conflict.ReapplyMine.BoundingBoxAsync())!;
        reload.Width.ShouldBeGreaterThan(banner.Width - 40);
        reapply.Width.ShouldBe(reload.Width, 1);
        (await TouchRules.ViolationsAsync(Page, conflict.Banner)).ShouldBeEmpty();

        await conflict.ReloadTheirs.TapAsync();

        await Expect(conflict.Banner).ToHaveCountAsync(0);
        await Expect(touch.SaveBar).ToHaveCountAsync(0);
        await Expect(screen.Points).ToHaveValueAsync("3");
    }
}
