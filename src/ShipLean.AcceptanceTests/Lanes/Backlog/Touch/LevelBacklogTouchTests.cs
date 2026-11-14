using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Touch;

// The Features and Epics backlogs on a phone (SHIP-716 D1, D8), on the seeded SHIP project. Nothing here changes data.
[TestFixture]
public class LevelBacklogTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Features_WhenOpenedOnPhone_ShouldShowProgressLineAndOpenItemOnTap()
    {
        var backlog = new LevelBacklog(Page);
        var peek = new ItemPeekDialog(Page);
        await backlog.OpenAsync("SHIP", "features");
        var list = Page.GetByTestId(BacklogTestIds.TouchRowList);

        await Expect(Page.GetByTestId(BacklogTestIds.ColumnHeader)).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId(BacklogTestIds.TouchRowHandle)).ToHaveCountAsync(0);
        await Expect(backlog.ProgressText("SHIP-101")).ToHaveTextAsync("4 / 8 done");
        await Expect(backlog.PointsSum("SHIP-101")).ToHaveTextAsync("· Σ26");
        await Expect(backlog.PointsSum("SHIP-101")).ToHaveAttributeAsync("data-warn", "true");
        var title = await backlog.Row("SHIP-101").GetByTestId(BacklogTestIds.Title).BoundingBoxAsync();
        var progress = await backlog.Row("SHIP-101").GetByTestId(BacklogTestIds.LevelTouchProgress).BoundingBoxAsync();
        progress!.Y.ShouldBeGreaterThanOrEqualTo(title!.Y + title.Height - 1);
        var overflow = await Page.EvaluateAsync<int>(
            "document.documentElement.scrollWidth > document.documentElement.clientWidth ? 1 : 0");
        overflow.ShouldBe(0);
        (await TouchRules.ViolationsAsync(Page, list)).ShouldBeEmpty();

        await backlog.Row("SHIP-101").GetByTestId(BacklogTestIds.LevelTouchMeta).TapAsync();

        await peek.WaitOpenAsync("SHIP-101");
        new Uri(Page.Url).Query.ShouldContain("item=SHIP-101");
    }

    [Test]
    public async Task Epics_WhenOpenedOnPhone_ShouldShowFeaturesCountOnEachRow()
    {
        var backlog = new LevelBacklog(Page);
        await backlog.OpenAsync("SHIP", "epics");

        await Expect(backlog.FeatureCount("SHIP-92")).ToHaveTextAsync("2 Features");
        await Expect(backlog.FeatureCount("SHIP-90")).ToHaveTextAsync("1 Features");
        (await TouchRules.ViolationsAsync(Page, Page.GetByTestId(BacklogTestIds.TouchRowList))).ShouldBeEmpty();
    }
}
