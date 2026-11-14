using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;
using ShipLean.TestSupport.Usage;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.FeatureBoard;

// Long-press drag of a task on Checkout v2 (SHIP-101) on a phone (SHIP-722). Each test moves a task of its own: SHIP-173
// (New, under Pay with saved card) goes to the Address autocomplete lane, SHIP-176 (New, under Address autocomplete) goes
// to the Active page by holding at the screen's edge.
[TestFixture]
public class FeatureBoardPhoneDragTests : SeededAcceptanceTest
{
    private const string Project = "SHIP";
    private const string Feature = "SHIP-101";

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private async Task<JsonElement> ItemAsync(string key)
    {
        var response = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{key}");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    private async Task<PhoneGridPart> OpenAsync()
    {
        var grid = new PhoneGridPart(Page);
        await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
        await grid.WaitAsync();
        return grid;
    }

    private Task<IResponse> WaitForMoveAsync(Func<Task> release) =>
        Page.RunAndWaitForResponseAsync(release, r =>
            r.Request.Method == "POST" && new Uri(r.Url).AbsolutePath.EndsWith("/feature-board/moves", StringComparison.Ordinal));

    [Test]
    public async Task FeatureBoard_WhenTaskLongPressDraggedToAnotherStoryOnPhone_ShouldMoveItAndRecordTouchDrag()
    {
        var grid = await OpenAsync();
        var actions = new PhoneTaskActionsPart(Page);
        var drag = new PhoneDragPart(Page);
        var card = actions.Card("SHIP-173");
        var lane = grid.LaneTitled(grid.PageNamed("New"), "Address autocomplete").GetByTestId(TaskboardTestIds.LaneTasks);
        Recorder.Clear();

        await drag.LiftAsync(card);
        (await TouchRules.ViolationsAsync(Page, card)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, drag.Ghost)).ShouldBeEmpty();
        await drag.MoveOverAsync(lane);
        await Expect(lane).ToHaveAttributeAsync("data-drop-active", string.Empty);
        var moved = await WaitForMoveAsync(drag.ReleaseAsync);

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        var after = await ItemAsync("SHIP-173");
        after.GetProperty("parent").GetProperty("key").GetString().ShouldBe("SHIP-144");
        after.GetProperty("state").GetProperty("name").GetString().ShouldBe("New");
        await Expect(lane.GetByTestId(TaskboardTestIds.Card).Filter(new() { HasText = "SHIP-173" })).ToHaveCountAsync(1);
        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.WorkItemMoved).ShouldHaveSingleItem();
        tracked.Properties[UsageProps.Method].ShouldBe(MoveMethods.TouchDrag);
        tracked.Properties[UsageProps.Viewport].ShouldBe(UsageViewports.Phone);
    }

    [Test]
    public async Task FeatureBoard_WhenTaskHeldAtScreenEdgeOnPhone_ShouldTurnToNextStateAndDropThere()
    {
        var grid = await OpenAsync();
        var actions = new PhoneTaskActionsPart(Page);
        var drag = new PhoneDragPart(Page);
        var card = actions.Card("SHIP-176");
        Recorder.Clear();

        var (_, y) = await drag.LiftAsync(card);
        await drag.HoldAtRightEdgeAsync(y);
        // The page turns again every 800ms while the finger stays at the edge: leave it as soon as this one is on screen.
        await Expect(grid.Chip("Active")).ToHaveAttributeAsync("aria-current", "true");
        await drag.MoveToMiddleAsync(y);
        await Expect(PhoneGridPart.PageHead(grid.PageNamed("Active"))).ToBeInViewportAsync(new() { Ratio = 1 });
        var lane = grid.LaneTitled(grid.PageNamed("Active"), "Address autocomplete").GetByTestId(TaskboardTestIds.LaneTasks);
        await drag.MoveOverAsync(lane);
        var moved = await WaitForMoveAsync(drag.ReleaseAsync);

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        var after = await ItemAsync("SHIP-176");
        after.GetProperty("state").GetProperty("name").GetString().ShouldBe("Active");
        after.GetProperty("parent").GetProperty("key").GetString().ShouldBe("SHIP-144");
        await Expect(grid.Chip("Active")).ToHaveAttributeAsync("aria-current", "true");
        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.WorkItemMoved).ShouldHaveSingleItem();
        tracked.Properties[UsageProps.Method].ShouldBe(MoveMethods.TouchDrag);
    }
}
