using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Backlog.BacklogApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// The Sprint backlog's rows on a phone (SHIP-724), on seeded Sprint 24 of SHIP. It changes states, so it has a class of its own.
[TestFixture]
public class SprintBacklogPhoneRowsTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task SprintBacklog_WhenStatePillTappedOnPhone_ShouldApplyStateFromSheet()
    {
        var backlog = new SprintBacklogPhoneRows(Page);
        await backlog.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(backlog.RowState("SHIP-182")).Not.ToHaveTextAsync("Closed");
        (await TouchRules.ViolationsAsync(Page, backlog.List)).ShouldBeEmpty();

        await backlog.Row("SHIP-182").GetByTestId(SprintsTestIds.StateButton).TapAsync();

        await Expect(backlog.Sheet).ToBeVisibleAsync();
        await Expect(backlog.Sheet.GetByTestId(SprintsTestIds.StatesSheetCategory).Last).ToHaveTextAsync("Completed");
        await backlog.Sheet.GetByTestId(SprintsTestIds.StatesSheetOption).Filter(new() { HasText = "Closed" }).TapAsync();

        await Expect(backlog.Sheet).ToHaveCountAsync(0);
        await Expect(backlog.RowState("SHIP-182")).ToHaveTextAsync("Closed");
        await Page.ReloadAsync();
        await backlog.List.WaitForAsync();
        await Expect(backlog.RowState("SHIP-182")).ToHaveTextAsync("Closed");
    }

    [Test]
    public async Task SprintBacklog_WhenStateChangedElsewhereOnPhone_ShouldToastAndFlashRow()
    {
        var backlog = new SprintBacklogPhoneRows(Page);
        await backlog.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(backlog.RowState("SHIP-172")).Not.ToHaveTextAsync("Closed");

        // The page keeps the list it has, so the teammate's change never reaches it, until the stale write has answered.
        var read = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items");
        var snapshot = await read.TextAsync();
        var frozen = true;
        await Page.RouteAsync(
            url => new Uri(url).AbsolutePath == $"/api/projects/{SeedIds.Projects.Ship}/work-items",
            route => frozen
                ? route.FulfillAsync(new() { ContentType = "application/json", Body = snapshot })
                : route.ContinueAsync());
        await Page.RouteAsync(
            url => new Uri(url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal),
            async route =>
            {
                var response = await route.FetchAsync();
                frozen = false;
                await route.FulfillAsync(new() { Response = response });
            });
        await SetStateAsync(Page, "SHIP-172", SeedIds.States.Ship.TaskNew);

        var stale = await backlog.PickStateAsync("SHIP-172", "Closed");

        stale.Status.ShouldBe(409);
        await Expect(backlog.Toast).ToContainTextAsync("SHIP-172 was changed by someone else");
        await Expect(backlog.Row("SHIP-172")).ToHaveAttributeAsync("data-flash", "true");
        await Expect(backlog.RowState("SHIP-172")).ToHaveTextAsync("New");
        await Expect(Page.GetByTestId(SprintsTestIds.StateNotice)).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SprintBacklog_WhenTasksSwitchedOffOnPhone_ShouldHideTaskRows()
    {
        var backlog = new SprintBacklogPhoneRows(Page);
        await backlog.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(backlog.TaskRows.First).ToBeVisibleAsync();
        await Expect(backlog.TasksSwitch).ToHaveAttributeAsync("aria-checked", "true");

        await backlog.TasksSwitch.TapAsync();

        await Expect(backlog.TasksSwitch).ToHaveAttributeAsync("aria-checked", "false");
        await Expect(backlog.TaskRows).ToHaveCountAsync(0);
        await Expect(backlog.StoryRows.First).ToBeVisibleAsync();
        await Expect(Page.GetByTestId(SprintsTestIds.UnparentedHeading)).ToHaveCountAsync(0);
        (await TouchRules.ViolationsAsync(Page, backlog.List)).ShouldBeEmpty();
        var doc = await Page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        doc.ShouldBeFalse();
    }
}
