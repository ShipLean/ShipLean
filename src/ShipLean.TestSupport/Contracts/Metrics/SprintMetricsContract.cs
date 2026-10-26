using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Metrics;

// Behaviour every ISprintMetrics must have, whatever it stores data in. The arranged history is three ended sprints that
// committed and completed 20/20, 24/22 and 23/21 points.
[TestFixture]
public abstract class SprintMetricsContract
{
    protected abstract ISprintMetrics CreateMetrics();

    protected abstract Task<Guid> ArrangeProjectAsync();

    protected abstract Task<Guid> ArrangeSprintAsync(Guid projectId, string name, DateOnly start, DateOnly end);

    // An item in the sprint (in the backlog when sprintId is null). completedOn set = it reached the Completed category on that day;
    // addedAfterStart = it joined the sprint on its first day (after 00:00 UTC), so it is added, not committed. Backlog order is the order items are arranged in.
    protected abstract Task<Guid> ArrangeItemAsync(Guid projectId, WorkItemTypeId type, decimal? points, Guid? sprintId, bool addedAfterStart, DateOnly? completedOn);

    // From that day on the item is in the sprint (in the backlog when toSprintId is null).
    protected abstract Task MoveItemAsync(Guid itemId, Guid? toSprintId, DateOnly on);

    // The item reaches the Completed category on that day.
    protected abstract Task CompleteItemAsync(Guid itemId, DateOnly on);

    // The item goes back to an In Progress state on that day (no longer completed).
    protected abstract Task ReopenItemAsync(Guid itemId, DateOnly on);

    // The item reaches the Removed category on that day.
    protected abstract Task RemoveItemAsync(Guid itemId, DateOnly on);

    // The sprint's team has this many points of capacity entered.
    protected abstract Task SetTeamCapacityAsync(Guid sprintId, decimal points);

    protected abstract Task SetPointsAsync(Guid itemId, decimal? points);

    protected abstract Task SetVelocityOverrideAsync(Guid projectId, decimal? value);

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    // Sprints of 13 days ending 1, 15, 29 ... days ago, oldest first; committed and completed points as given.
    private async Task<(Guid Project, List<Guid> Sprints)> ArrangeHistoryAsync(params (decimal Committed, decimal Completed)[] sprints)
    {
        var project = await ArrangeProjectAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < sprints.Length; i++)
        {
            var end = Today.AddDays(-1 - 14 * (sprints.Length - 1 - i));
            var start = end.AddDays(-13);
            var sprint = await ArrangeSprintAsync(project, $"Sprint {i + 1}", start, end);
            ids.Add(sprint);
            await ArrangeItemAsync(project, WorkItemTypeId.Story, sprints[i].Completed, sprint, false, end.AddDays(-4));
            if (sprints[i].Committed > sprints[i].Completed)
            {
                await ArrangeItemAsync(project, WorkItemTypeId.Bug, sprints[i].Committed - sprints[i].Completed, sprint, false, null);
            }
        }
        return (project, ids);
    }

    [Test]
    public async Task Velocity_WhenThreeSprints_ShouldAverageCompleted()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20), (24, 22), (23, 21));

        var velocity = await CreateMetrics().GetVelocityAsync(project);

        velocity.Window.ShouldBe(3);
        velocity.Sprints.Select(s => (s.SprintId, s.Committed, s.Completed)).ShouldBe([(sprints[0], 20m, 20m), (sprints[1], 24m, 22m), (sprints[2], 23m, 21m)]);
        velocity.Average.ShouldBe(21m);
        velocity.Override.ShouldBeNull();
    }

    [Test]
    public async Task Velocity_WhenMoreSprintsThanTheWindow_ShouldUseTheLatest()
    {
        var (project, _) = await ArrangeHistoryAsync((50, 50), (20, 20), (24, 22), (23, 21));

        var velocity = await CreateMetrics().GetVelocityAsync(project);

        velocity.Sprints.Count.ShouldBe(3);
        velocity.Average.ShouldBe(21m);
    }

    [Test]
    public async Task Velocity_WhenOverrideSet_ShouldCarryIt()
    {
        var (project, _) = await ArrangeHistoryAsync((20, 20));
        await SetVelocityOverrideAsync(project, 30m);

        var velocity = await CreateMetrics().GetVelocityAsync(project);

        velocity.Override.ShouldBe(30m);
        velocity.Average.ShouldBe(20m);
    }

    [Test]
    public async Task Metrics_WhenTaskPointsChange_ShouldNotMove()
    {
        var (project, sprints) = await ArrangeHistoryAsync((24, 22));
        var metrics = CreateMetrics();
        var task = await ArrangeItemAsync(project, WorkItemTypeId.Task, 5, sprints[0], false, null);
        var before = await metrics.GetSprintAsync(sprints[0]);

        await SetPointsAsync(task, 13);
        var after = await metrics.GetSprintAsync(sprints[0]);

        (before.Committed, before.Completed).ShouldBe((24m, 22m));
        (after.Committed, after.Completed, after.Remaining).ShouldBe((before.Committed, before.Completed, before.Remaining));
        (await metrics.GetVelocityAsync(project)).Average.ShouldBe(22m);
    }

    [Test]
    public async Task Metrics_WhenItemAddedMidSprintAndCompleted_ShouldCountAsCompletedNotCommitted()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));
        var completedOn = Today.AddDays(-5);

        await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, sprints[0], addedAfterStart: true, completedOn: completedOn);
        var metrics = await CreateMetrics().GetSprintAsync(sprints[0]);

        metrics.Committed.ShouldBe(20m);
        metrics.Completed.ShouldBe(23m);
    }

    [Test]
    public async Task Metrics_WhenItemCompletedOutsideTheSprint_ShouldNotCount()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));
        var window = Today.AddDays(-5);
        var next = await ArrangeSprintAsync(project, "Next", Today.AddDays(1), Today.AddDays(14));
        await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, null, false, window);
        await ArrangeItemAsync(project, WorkItemTypeId.Story, 2, next, false, window);

        var metrics = await CreateMetrics().GetSprintAsync(sprints[0]);

        (metrics.Committed, metrics.Completed).ShouldBe((20m, 20m));
    }

    [Test]
    public async Task Metrics_WhenJoinedOnTheFirstDay_ShouldCountAsAddedNotCommitted()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));
        var first = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, null, false, null);

        await MoveItemAsync(first, sprints[0], Today.AddDays(-14));
        var metrics = await CreateMetrics().GetSprintAsync(sprints[0]);

        metrics.Committed.ShouldBe(20m);
        metrics.Remaining.ShouldBe(3m);
    }

    [Test]
    public async Task Metrics_WhenUnfinishedItemRolledOver_ShouldStayCommittedInEndedSprint()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));
        var metrics = CreateMetrics();
        var carried = await ArrangeItemAsync(project, WorkItemTypeId.Story, 4, sprints[0], false, null);
        var next = await ArrangeSprintAsync(project, "Next", Today.AddDays(1), Today.AddDays(14));

        await MoveItemAsync(carried, next, Today);

        var ended = await metrics.GetSprintAsync(sprints[0]);
        (ended.Committed, ended.Completed, ended.Remaining).ShouldBe((24m, 20m, 4m));
        (await metrics.GetSprintAsync(next)).Committed.ShouldBe(4m);
        (await metrics.GetVelocityAsync(project)).Sprints.Single().Committed.ShouldBe(24m);
    }

    [Test]
    public async Task Metrics_WhenCompletedAfterSprintEnded_ShouldNotCount()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));
        await ArrangeItemAsync(project, WorkItemTypeId.Story, 5, sprints[0], false, Today);
        var metrics = CreateMetrics();

        var sprint = await metrics.GetSprintAsync(sprints[0]);

        (sprint.Committed, sprint.Completed).ShouldBe((25m, 20m));
        var entry = (await metrics.GetVelocityAsync(project)).Sprints.Single();
        (entry.Committed, entry.Completed).ShouldBe((25m, 20m));
    }

    [Test]
    public async Task Burndown_WhenItemAddedMidSprint_ShouldRaiseRemainingFromThatDay()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 12));
        var metrics = CreateMetrics();
        var before = (await metrics.GetSprintAsync(sprints[0])).Burndown;
        var addedOn = before[0].Date.AddDays(5);
        var late = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, null, false, null);

        await MoveItemAsync(late, sprints[0], addedOn);

        var after = (await metrics.GetSprintAsync(sprints[0])).Burndown;
        after.Select(p => p.Date).ShouldBe(before.Select(p => p.Date));
        after.Where(p => p.Date < addedOn).Select(p => p.Remaining).ShouldBe(before.Where(p => p.Date < addedOn).Select(p => p.Remaining));
        after.Where(p => p.Date >= addedOn).Select(p => p.Remaining).ShouldBe(before.Where(p => p.Date >= addedOn).Select(p => p.Remaining + 3));
        after.Any(p => p.Date < addedOn).ShouldBeTrue();
        after.Any(p => p.Date >= addedOn).ShouldBeTrue();
        after.Select(p => p.Ideal).ShouldBe(before.Select(p => p.Ideal));
    }

    [Test]
    public async Task Burndown_WhenTaskStateChanges_ShouldNotMove()
    {
        var (project, sprints) = await ArrangeHistoryAsync((24, 22));
        var task = await ArrangeItemAsync(project, WorkItemTypeId.Task, 5, sprints[0], false, null);
        var metrics = CreateMetrics();
        var before = await metrics.GetSprintAsync(sprints[0]);

        await CompleteItemAsync(task, before.Burndown[2].Date);
        var after = await metrics.GetSprintAsync(sprints[0]);

        after.Burndown.ShouldBe(before.Burndown);
        (after.Committed, after.Completed, after.Remaining).ShouldBe((before.Committed, before.Completed, before.Remaining));
    }

    [Test]
    public async Task Burndown_WhenItemRemoved_ShouldLeaveRemaining()
    {
        var (project, sprints) = await ArrangeHistoryAsync((24, 22));
        var metrics = CreateMetrics();
        var before = await metrics.GetSprintAsync(sprints[0]);
        var removedOn = before.Burndown[3].Date;
        var open = await ArrangeItemAsync(project, WorkItemTypeId.Story, 2, sprints[0], false, null);

        await RemoveItemAsync(open, removedOn);
        var after = await metrics.GetSprintAsync(sprints[0]);

        after.Burndown.Select(p => p.Date).ShouldBe(before.Burndown.Select(p => p.Date));
        after.Burndown.Where(p => p.Date < removedOn).Select(p => p.Remaining).ShouldBe(before.Burndown.Where(p => p.Date < removedOn).Select(p => p.Remaining + 2));
        after.Burndown.Where(p => p.Date >= removedOn).Select(p => p.Remaining).ShouldBe(before.Burndown.Where(p => p.Date >= removedOn).Select(p => p.Remaining));
        (after.Committed, after.Completed, after.Remaining).ShouldBe((26m, 22m, before.Remaining));
    }

    [Test]
    public async Task Completed_WhenReopenedAndLeftOpen_ShouldNotCount()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));
        var reopened = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, sprints[0], false, Today.AddDays(-8));
        var metrics = CreateMetrics();
        (await metrics.GetSprintAsync(sprints[0])).Completed.ShouldBe(23m);

        await ReopenItemAsync(reopened, Today.AddDays(-5));

        var sprint = await metrics.GetSprintAsync(sprints[0]);
        (sprint.Committed, sprint.Completed, sprint.Remaining).ShouldBe((23m, 20m, 3m));
        (await metrics.GetVelocityAsync(project)).Average.ShouldBe(20m);
    }

    [Test]
    public async Task Completed_WhenReopenedAndCompletedAgain_ShouldCountOnce()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));
        var again = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, sprints[0], false, Today.AddDays(-8));

        await ReopenItemAsync(again, Today.AddDays(-6));
        await CompleteItemAsync(again, Today.AddDays(-4));

        var sprint = await CreateMetrics().GetSprintAsync(sprints[0]);
        (sprint.Committed, sprint.Completed, sprint.Remaining).ShouldBe((23m, 23m, 0m));
    }

    [Test]
    public async Task Completed_WhenCompletedThenRemoved_ShouldNotCount()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));
        var dropped = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, sprints[0], false, Today.AddDays(-8));

        await RemoveItemAsync(dropped, Today.AddDays(-5));

        var sprint = await CreateMetrics().GetSprintAsync(sprints[0]);
        (sprint.Committed, sprint.Completed, sprint.Remaining).ShouldBe((23m, 20m, 0m));
    }

    [Test]
    public async Task Completed_WhenReopenedAfterLeavingTheSprint_ShouldStillCount()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));
        var left = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, sprints[0], false, Today.AddDays(-8));

        await MoveItemAsync(left, null, Today.AddDays(-6));
        await ReopenItemAsync(left, Today.AddDays(-4));

        (await CreateMetrics().GetSprintAsync(sprints[0])).Completed.ShouldBe(23m);
    }

    [Test]
    public async Task Forecast_WhenVelocityZero_ShouldPlaceOneItemPerSprint()
    {
        var project = await ArrangeProjectAsync();
        var items = new List<Guid>
        {
            await ArrangeItemAsync(project, WorkItemTypeId.Story, 5, null, false, null),
            await ArrangeItemAsync(project, WorkItemTypeId.Story, null, null, false, null),
            await ArrangeItemAsync(project, WorkItemTypeId.Bug, 2, null, false, null),
        };

        var forecast = await CreateMetrics().GetForecastAsync(project);

        forecast.Sprints.Select(s => s.ItemCount).ShouldBe([1, 1, 1]);
        forecast.Sprints.Select(s => s.LastItemId).ShouldBe(items.Select(id => (Guid?)id));
        forecast.Sprints.Select(s => s.Room).ShouldAllBe(room => room == 0m);
    }

    [Test]
    public async Task Forecast_WhenCapacityEntered_ShouldUseRoomLeft()
    {
        var project = await ArrangeProjectAsync();
        await SetVelocityOverrideAsync(project, 20m);
        var planned = await ArrangeSprintAsync(project, "Sprint 25", Today.AddDays(5), Today.AddDays(18));
        await SetTeamCapacityAsync(planned, 12m);
        await ArrangeItemAsync(project, WorkItemTypeId.Story, 4, planned, false, null);
        var first = await ArrangeItemAsync(project, WorkItemTypeId.Story, 5, null, false, null);
        await ArrangeItemAsync(project, WorkItemTypeId.Story, 5, null, false, null);
        await ArrangeItemAsync(project, WorkItemTypeId.Bug, 5, null, false, null);

        var forecast = await CreateMetrics().GetForecastAsync(project);

        var inSprint = forecast.Sprints[0];
        (inSprint.SprintId, inSprint.Name, inSprint.Room, inSprint.FromCapacity).ShouldBe((planned, "Sprint 25", 8m, true));
        (inSprint.LastItemId, inSprint.ItemCount, inSprint.Points).ShouldBe((first, 1, 5m));
        forecast.Sprints.Count.ShouldBe(2);
        var after = forecast.Sprints[1];
        (after.SprintId, after.Name, after.Room, after.FromCapacity, after.ItemCount, after.Points).ShouldBe((null, "Sprint 26", 20m, false, 2, 10m));
    }

    [Test]
    public async Task Forecast_WhenBacklogOutlastsSprints_ShouldAddSprintsAtVelocity()
    {
        var project = await ArrangeProjectAsync();
        await SetVelocityOverrideAsync(project, 10m);
        var planned = await ArrangeSprintAsync(project, "Sprint 25", Today.AddDays(5), Today.AddDays(18));
        var items = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            items.Add(await ArrangeItemAsync(project, WorkItemTypeId.Story, 6, null, false, null));
        }

        var forecast = await CreateMetrics().GetForecastAsync(project);

        forecast.Sprints.Select(s => s.Name).ShouldBe(["Sprint 25", "Sprint 26", "Sprint 27", "Sprint 28"]);
        forecast.Sprints.Select(s => s.SprintId).ShouldBe([planned, null, null, null]);
        forecast.Sprints.Select(s => s.Room).ShouldAllBe(room => room == 10m);
        forecast.Sprints.Select(s => s.FromCapacity).ShouldAllBe(fromCapacity => !fromCapacity);
        forecast.Sprints.Select(s => s.LastItemId).ShouldBe(items.Select(id => (Guid?)id));
    }

    [Test]
    public async Task Forecast_WhenItemBiggerThanRoom_ShouldStillLandInAnEmptySprint()
    {
        var project = await ArrangeProjectAsync();
        await SetVelocityOverrideAsync(project, 10m);
        var huge = await ArrangeItemAsync(project, WorkItemTypeId.Story, 25, null, false, null);
        var small = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, null, false, null);

        var forecast = await CreateMetrics().GetForecastAsync(project);

        forecast.Sprints.Count.ShouldBe(2);
        (forecast.Sprints[0].LastItemId, forecast.Sprints[0].ItemCount, forecast.Sprints[0].Points).ShouldBe((huge, 1, 25m));
        (forecast.Sprints[1].LastItemId, forecast.Sprints[1].ItemCount, forecast.Sprints[1].Points).ShouldBe((small, 1, 3m));
    }

    [Test]
    public async Task Forecast_WhenUnestimated_ShouldCountZeroAndFlag()
    {
        var project = await ArrangeProjectAsync();
        await SetVelocityOverrideAsync(project, 10m);
        await ArrangeItemAsync(project, WorkItemTypeId.Story, null, null, false, null);
        await ArrangeItemAsync(project, WorkItemTypeId.Bug, 5, null, false, null);
        await ArrangeItemAsync(project, WorkItemTypeId.Story, null, null, false, null);

        var forecast = await CreateMetrics().GetForecastAsync(project);

        var only = forecast.Sprints.Single();
        (only.ItemCount, only.Points, only.UnestimatedCount).ShouldBe((3, 5m, 2));
    }

    [Test]
    public async Task Metrics_WhenSprintRuns_ShouldBurnDownPerWorkingDay()
    {
        var (project, sprints) = await ArrangeHistoryAsync((20, 20));

        var metrics = await CreateMetrics().GetSprintAsync(sprints[0]);

        metrics.Burndown.Count.ShouldBe(10);
        metrics.Burndown.Select(p => p.Date).ShouldBe(metrics.Burndown.Select(p => p.Date).Order().ToList());
        metrics.Burndown[0].Ideal.ShouldBe(20m);
        metrics.Burndown[^1].Ideal.ShouldBe(0m);
        metrics.Burndown[^1].Remaining.ShouldBe(0m);
        project.ShouldNotBe(Guid.Empty);
    }
}
