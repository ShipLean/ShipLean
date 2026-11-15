using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.Backlog.Bulk;
using ShipLean.AcceptanceTests.Lanes.Backlog.Interactions;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.Backlog.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Touch;

// The row ⋯ and the ? chip on the Stories List at a phone width (SHIP-716 D9), against the real K1 and the work item
// PATCH. Each test changes rows of its own, and only this task's own regions are checked for touch rules.
[TestFixture]
public class RowActionsTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Points_WhenSetFromQuestionChipOnPhone_ShouldEstimateRow()
    {
        var backlog = new StoriesBacklog(Page);
        var actions = new RowActions(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var row = actions.UnestimatedRows.First;
        var key = (await row.GetByTestId(BacklogTestIds.Key).InnerTextAsync()).Trim();
        var api = Page.Context.APIRequest;
        (await BulkReadBack.ReadAsync(api, key)).Points.ShouldBeNull();

        await row.GetByTestId(BacklogTestIds.Points).TapAsync();

        await Expect(actions.PointsSheet).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, actions.Sheet)).ShouldBeEmpty();
        (await actions.PointsInput.EvaluateAsync<double>("element => parseFloat(getComputedStyle(element).fontSize)")).ShouldBeGreaterThanOrEqualTo(16);
        await actions.PointsInput.FillAsync("13");
        var saved = await Page.RunAndWaitForResponseAsync(
            async () => await actions.PointsSave.TapAsync(),
            response => response.Request.Method == "PATCH" && response.Url.Contains("/api/work-items/", StringComparison.Ordinal));
        saved.Ok.ShouldBeTrue(await saved.TextAsync());

        await Expect(actions.PointsSheet).ToHaveCountAsync(0);
        await Expect(actions.Row(key).GetByTestId(BacklogTestIds.Points)).ToHaveTextAsync("13");
        (await BulkReadBack.ReadAsync(api, key)).Points.ShouldBe(13m);
    }

    [Test]
    public async Task Row_WhenMovedToTopFromSheetOnPhone_ShouldRankFirst()
    {
        var backlog = new StoriesBacklog(Page);
        var actions = new RowActions(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        await new Pages.Realtime.LiveStatus(Page).WaitUntilLiveAsync();
        var keys = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            keys.Add((await backlog.RowKeys.Nth(i).InnerTextAsync()).Trim());
        }

        var api = Page.Context.APIRequest;
        var moved = keys[3];
        (await BulkReadBack.InOrderAsync(api, [.. keys]))[0].ShouldNotBe(moved);

        await actions.OpenMoreAsync(actions.Row(moved));
        (await TouchRules.ViolationsAsync(Page, actions.Sheet)).ShouldBeEmpty();
        await actions.PickAsync("Move to top");

        await Expect(actions.Sheet).ToHaveCountAsync(0);
        await Expect(backlog.RowKeys.First).ToHaveTextAsync(moved);
        (await BulkReadBack.InOrderAsync(api, [.. keys]))[0].ShouldBe(moved);
    }

    [Test]
    public async Task Parent_WhenSheetOpenedOnPhone_ShouldMeetTouchRules()
    {
        var backlog = new StoriesBacklog(Page);
        var actions = new RowActions(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();

        await actions.OpenMoreAsync(backlog.Rows.First);
        await actions.PickAsync("Parent");

        await Expect(actions.ParentSheet).ToBeVisibleAsync();
        await Expect(actions.ParentSheet.GetByTestId(WorkItemTestIds.ParentOption).Nth(1)).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, actions.Sheet)).ShouldBeEmpty();
    }

    [Test]
    public async Task Queue_WhenOpenedOnPhone_ShouldMeetTouchRulesAndSkipToNext()
    {
        var backlog = new StoriesBacklog(Page);
        var actions = new RowActions(Page);
        await RowActions.AddUnestimatedStoriesAsync(Page.Context.APIRequest, SeedIds.Projects.Ship, 3);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();

        await actions.UnestimatedCheck.TapAsync();

        await Expect(actions.QueueBanner).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, actions.QueueBanner)).ShouldBeEmpty();
        var first = (await actions.QueueStepKey.InnerTextAsync()).Trim();
        await actions.QueueSkip.TapAsync();
        await Expect(actions.QueueStepKey).Not.ToHaveTextAsync(first);
    }
}
