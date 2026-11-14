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

// Moving tasks on Checkout v2 (SHIP-101) on a phone from the task's ⋯ sheet (SHIP-722). The tests run in order on one
// seeded project, signed in as Joel: SHIP-173 (New, under Pay with saved card) goes to Active, SHIP-176 (New, under Address
// autocomplete) goes to Pay with saved card and then to Priya. Each journey starts from the first state column.
[TestFixture]
public class FeatureBoardPhoneMovesTests : SeededAcceptanceTest
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

    private async Task OpenSheetAsync(string key)
    {
        var actions = new PhoneTaskActionsPart(Page);
        await Page.GotoAsync(Links.FeatureBoard(Project, Feature));
        await actions.Card(key).WaitForAsync();
        await actions.OpenAsync(key);
        await Expect(actions.Sheet).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, actions.Sheet)).ShouldBeEmpty();
    }

    [Test]
    [Order(1)]
    public async Task FeatureBoard_WhenTaskMovedFromSheetOnPhone_ShouldChangeStateAndRecordSheetMethod()
    {
        var actions = new PhoneTaskActionsPart(Page);
        Recorder.Clear();
        await OpenSheetAsync("SHIP-173");

        var moved = await actions.MoveToAsync("Active");

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        await Expect(actions.Sheet).ToHaveCountAsync(0);
        (await ItemAsync("SHIP-173")).GetProperty("state").GetProperty("name").GetString().ShouldBe("Active");
        var tracked = Recorder.Events.Where(e => e.Name == UsageEventNames.WorkItemMoved).ShouldHaveSingleItem();
        tracked.Properties[UsageProps.Method].ShouldBe(MoveMethods.Sheet);
        tracked.Properties[UsageProps.Viewport].ShouldBe(UsageViewports.Phone);
    }

    [Test]
    [Order(2)]
    public async Task FeatureBoard_WhenTaskMovedToAnotherStoryOnPhone_ShouldReparentAndKeepSprint()
    {
        var actions = new PhoneTaskActionsPart(Page);
        var before = await ItemAsync("SHIP-176");
        before.GetProperty("parent").GetProperty("key").GetString().ShouldBe("SHIP-144");
        await OpenSheetAsync("SHIP-176");

        var moved = await actions.MoveToStoryAsync("SHIP-141");

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        var after = await ItemAsync("SHIP-176");
        after.GetProperty("parent").GetProperty("key").GetString().ShouldBe("SHIP-141");
        after.GetProperty("state").GetProperty("name").GetString().ShouldBe("New");
        after.GetProperty("sprint").ToString().ShouldBe(before.GetProperty("sprint").ToString());
    }

    [Test]
    [Order(3)]
    public async Task FeatureBoard_WhenAssigneePickedOnPhone_ShouldAssignTheTask()
    {
        var actions = new PhoneTaskActionsPart(Page);
        await OpenSheetAsync("SHIP-176");

        var moved = await actions.AssignToAsync("Priya");

        moved.Ok.ShouldBeTrue(await moved.TextAsync());
        (await ItemAsync("SHIP-176")).GetProperty("assignee").GetProperty("id").GetGuid().ShouldBe(SeedIds.Users.Priya);
    }
}
