using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Backlog.BacklogApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// Changes states from the rows of Sprint 24, so it has a class of its own.
[TestFixture]
public class SprintBacklogStateChangeTests : SeededAcceptanceTest
{
    [Test]
    public async Task SprintBacklog_WhenStateChangedInPlace_ShouldPersist()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        var backlog = new SprintBacklogPage(Page);
        await backlog.OpenAsync("SHIP", sprint);
        var before = await ReadAsync(Page, $"/api/sprints/{sprint}/metrics");
        var story = await ReadAsync(Page, $"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-144");
        var points = story.GetProperty("points").GetDecimal();

        var closedStory = await backlog.PickStateAsync("SHIP-144", "Closed");
        closedStory.Ok.ShouldBeTrue(await closedStory.TextAsync());
        var closedTask = await backlog.PickStateAsync("SHIP-172", "Closed");
        closedTask.Ok.ShouldBeTrue(await closedTask.TextAsync());

        await Expect(backlog.RowState("SHIP-144")).ToHaveTextAsync("Closed");
        await Expect(backlog.RowState("SHIP-172")).ToHaveTextAsync("Closed");
        await Expect(backlog.Completed).ToHaveTextAsync($"{Pts(before.GetProperty("completed").GetDecimal() + points)}pts");
        await Expect(backlog.Remaining).ToHaveTextAsync($"{Pts(before.GetProperty("remaining").GetDecimal() - points)}pts");

        await Page.ReloadAsync();
        await backlog.Committed.WaitForAsync();
        await Expect(backlog.RowState("SHIP-144")).ToHaveTextAsync("Closed");
        await Expect(backlog.RowState("SHIP-172")).ToHaveTextAsync("Closed");
    }
}
