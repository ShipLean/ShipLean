using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;
using static ShipLean.AcceptanceTests.Lanes.Sprints.Backlog.BacklogApi;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// The seeded Storefront's current Sprint 24, signed in as Joel.
[TestFixture]
public class SprintBacklogTests : SeededAcceptanceTest
{
    [Test]
    public async Task SprintBacklog_WhenOpened_ShouldShowCommittedVsCapacity()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        var metrics = await ReadAsync(Page, $"/api/sprints/{sprint}/metrics");
        var capacity = await ReadAsync(Page, $"/api/sprints/{sprint}/capacity");
        var committed = metrics.GetProperty("committed").GetDecimal();
        var teamTotal = capacity.GetProperty("teamTotal").GetDecimal();
        capacity.GetProperty("members").GetArrayLength().ShouldBeGreaterThan(0);

        var backlog = new SprintBacklogPage(Page);
        await backlog.OpenAsync("SHIP", sprint);

        await Expect(backlog.Committed).ToHaveTextAsync($"{Pts(committed)}/ {Pts(teamTotal)} pts");
        await Expect(backlog.StoryRows).ToHaveCountAsync(5);
        await Expect(backlog.StoryRow("SHIP-141")).ToContainTextAsync("Pay with saved card");
        await Expect(backlog.StoryRow("SHIP-129")).ToContainTextAsync("☑ 2/4");
        await Expect(backlog.TaskRows.Filter(new() { HasText = "SHIP-172" })).ToBeVisibleAsync();
        await Expect(backlog.DaysLeft).Not.ToHaveTextAsync(string.Empty);
    }

    [Test]
    public async Task SprintBacklog_WhenUnparentedTasks_ShouldListThemLast()
    {
        var backlog = new SprintBacklogPage(Page);
        await backlog.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);

        await Expect(backlog.UnparentedHeading).ToBeVisibleAsync();
        await Expect(backlog.TaskRows.Last).ToContainTextAsync("SHIP-183");
        await Expect(backlog.TaskRows.Nth(-2)).ToContainTextAsync("SHIP-182");
        // Both follow the heading, which follows every story and its tasks.
        var order = await backlog.RowKindsAsync();
        var headingAt = Array.IndexOf(order, "sprints-backlog-unparented-heading");
        order.Skip(headingAt + 1).ShouldAllBe(id => id == "sprints-backlog-task-row");
        order.Skip(headingAt + 1).Count().ShouldBe(2);
        order.Take(headingAt).ShouldContain("sprints-backlog-story-row");
    }
}
