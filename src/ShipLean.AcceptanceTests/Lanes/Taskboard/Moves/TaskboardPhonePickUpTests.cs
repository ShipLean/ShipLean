using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;
using ShipLean.TestSupport.Usage;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.Moves;

// Pick up and No parent on the sprint Taskboard on a phone (SHIP-725 D12, D13): sprint 24 of the seeded Storefront project,
// signed in as Joel. SHIP-173 (New, unassigned) is picked up; SHIP-179 (New, under a story) is moved to No parent.
[TestFixture]
public class TaskboardPhonePickUpTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private async Task<JsonElement> ItemAsync(string key)
    {
        var response = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{key}");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        return JsonDocument.Parse(await response.TextAsync()).RootElement.Clone();
    }

    private async Task<PhoneTaskActionsPart> OpenBoardAsync(string key)
    {
        var actions = new PhoneTaskActionsPart(Page);
        await Page.GotoAsync(Links.Taskboard("SHIP", SeedIds.Sprints.Sprint24));
        await new PhoneGridPart(Page).WaitAsync();
        await actions.Card(key).WaitForAsync();
        return actions;
    }

    [Test]
    public async Task Taskboard_WhenPickUpTappedOnPhone_ShouldAssignMeStartItAndRecordPickUp()
    {
        var actions = await OpenBoardAsync("SHIP-173");
        Recorder.Clear();
        var pickUp = actions.Card("SHIP-173").GetByTestId(TaskboardTestIds.CardPickUp);
        await Expect(pickUp).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, pickUp)).ShouldBeEmpty();

        var moved = await WaitForMoveAsync(() => pickUp.TapAsync());

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        var item = await ItemAsync("SHIP-173");
        item.GetProperty("assignee").GetProperty("id").GetGuid().ShouldBe(SeedIds.Users.Joel);
        item.GetProperty("state").GetProperty("category").GetString().ShouldBe("inProgress");
        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.WorkItemMoved).ShouldHaveSingleItem();
        tracked.Properties[UsageProps.Method].ShouldBe(MoveMethods.PickUp);
        tracked.Properties[UsageProps.Viewport].ShouldBe(UsageViewports.Phone);
    }

    [Test]
    public async Task Taskboard_WhenTaskMovedToNoParentOnPhone_ShouldPutItInUnparented()
    {
        var actions = await OpenBoardAsync("SHIP-179");
        (await ItemAsync("SHIP-179")).GetProperty("parent").ValueKind.ShouldNotBe(JsonValueKind.Null);
        await actions.OpenAsync("SHIP-179");
        await ShowStoryPickerAsync(actions);

        var noParent = actions.Sheet.GetByTestId(TaskboardTestIds.StoryNoParent);
        await Expect(noParent).ToBeVisibleAsync();
        var moved = await WaitForMoveAsync(() => noParent.TapAsync());

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        JsonDocument.Parse(moved.Request.PostData!).RootElement.GetProperty("method").GetString().ShouldBe(MoveMethods.Sheet);
        var after = await ItemAsync("SHIP-179");
        after.TryGetProperty("parent", out var parent).ShouldBeTrue();
        parent.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    private Task<IResponse> WaitForMoveAsync(Func<Task> action) =>
        Page.RunAndWaitForResponseAsync(action, r =>
            r.Request.Method == "POST" && new Uri(r.Url).AbsolutePath.EndsWith("/taskboard/moves", StringComparison.Ordinal));

    private static Task ShowStoryPickerAsync(PhoneTaskActionsPart actions) =>
        actions.Sheet.GetByTestId(TouchTestIds.ActionSheetRow).Filter(new() { HasTextString = "Move to another story" }).TapAsync();
}
