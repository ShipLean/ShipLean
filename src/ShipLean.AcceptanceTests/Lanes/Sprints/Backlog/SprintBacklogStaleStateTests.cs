using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Realtime;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Backlog.BacklogApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// A state picked against a revision a teammate has since moved on. Its own class: it changes a state.
[TestFixture]
public class SprintBacklogStaleStateTests : SeededAcceptanceTest
{
    [Test]
    public async Task SprintBacklog_WhenStaleStateChange_ShouldRefetchRow()
    {
        var backlog = new SprintBacklogPage(Page);
        await backlog.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await new LiveStatus(Page).WaitUntilLiveAsync();
        await Expect(backlog.RowState("SHIP-172")).ToHaveTextAsync("Active");

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
        await Expect(backlog.StateNotice).ToContainTextAsync("SHIP-172 was changed by someone else");
        await Expect(backlog.RowState("SHIP-172")).ToHaveTextAsync("New");
    }
}
