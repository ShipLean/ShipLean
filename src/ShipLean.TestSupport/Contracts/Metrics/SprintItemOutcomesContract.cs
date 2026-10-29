using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Metrics;

// Behaviour of ISprintMetrics.GetSprintItemsAsync for every implementation. One ended sprint of 14 days; items are arranged
// around it (committed = in it before its first day) and each case reads the flags of its own item.
[TestFixture]
public abstract class SprintItemOutcomesContract
{
    protected abstract ISprintMetrics CreateMetrics();

    protected abstract Task<Guid> ArrangeProjectAsync();

    protected abstract Task<Guid> ArrangeSprintAsync(Guid projectId, string name, DateOnly start, DateOnly end);

    // addedAfterStart = it joined the sprint on its first day, so it is added, not committed.
    protected abstract Task<Guid> ArrangeItemAsync(Guid projectId, WorkItemTypeId type, decimal? points, Guid? sprintId, bool addedAfterStart, DateOnly? completedOn);

    protected abstract Task MoveItemAsync(Guid itemId, Guid? toSprintId, DateOnly on);

    protected abstract Task CompleteItemAsync(Guid itemId, DateOnly on);

    protected abstract Task ReopenItemAsync(Guid itemId, DateOnly on);

    protected abstract Task RemoveItemAsync(Guid itemId, DateOnly on);

    private static DateOnly Today => DateOnly.FromDateTime(TimeProvider.System.GetUtcNow().UtcDateTime);

    private async Task<(Guid Project, Guid Sprint, DateOnly Start, DateOnly End)> ArrangeSprintOnlyAsync()
    {
        var project = await ArrangeProjectAsync();
        var end = Today.AddDays(-20);
        var start = end.AddDays(-13);
        return (project, await ArrangeSprintAsync(project, "Outcome Sprint", start, end), start, end);
    }

    private async Task<SprintItemOutcomeDto?> OutcomeAsync(Guid sprint, Guid item)
        => (await CreateMetrics().GetSprintItemsAsync(sprint)).SingleOrDefault(o => o.Id == item);

    // Committed, added, completed, carried.
    private static (bool, bool, bool, bool) Flags(SprintItemOutcomeDto? outcome)
    {
        outcome.ShouldNotBeNull();
        return (outcome.Committed, outcome.AddedMidSprint, outcome.Completed, outcome.CarriedOver);
    }

    [Test]
    public async Task GetSprintItems_WhenCommittedAndCompleted_ShouldNotBeCarried()
    {
        var (project, sprint, _, end) = await ArrangeSprintOnlyAsync();
        var item = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, sprint, false, end.AddDays(-4));

        Flags(await OutcomeAsync(sprint, item)).ShouldBe((true, false, true, false));
    }

    [Test]
    public async Task GetSprintItems_WhenCommittedAndStillOpen_ShouldBeCarried()
    {
        var (project, sprint, _, _) = await ArrangeSprintOnlyAsync();
        var item = await ArrangeItemAsync(project, WorkItemTypeId.Bug, 2, sprint, false, null);

        Flags(await OutcomeAsync(sprint, item)).ShouldBe((true, false, false, true));
    }

    [Test]
    public async Task GetSprintItems_WhenJoinedAfterTheStartAndOpen_ShouldBeAddedAndCarried()
    {
        var (project, sprint, start, _) = await ArrangeSprintOnlyAsync();
        var item = await ArrangeItemAsync(project, WorkItemTypeId.Story, 2, null, false, null);
        await MoveItemAsync(item, sprint, start.AddDays(3));

        Flags(await OutcomeAsync(sprint, item)).ShouldBe((false, true, false, true));
    }

    [Test]
    public async Task GetSprintItems_WhenJoinedAfterTheStartAndCompleted_ShouldBeAddedNotCarried()
    {
        var (project, sprint, start, _) = await ArrangeSprintOnlyAsync();
        var item = await ArrangeItemAsync(project, WorkItemTypeId.Story, 2, null, false, null);
        await MoveItemAsync(item, sprint, start.AddDays(3));
        await CompleteItemAsync(item, start.AddDays(5));

        Flags(await OutcomeAsync(sprint, item)).ShouldBe((false, true, true, false));
    }

    [Test]
    public async Task GetSprintItems_WhenCompletedThenReopened_ShouldBeCarriedNotCompleted()
    {
        var (project, sprint, start, _) = await ArrangeSprintOnlyAsync();
        var item = await ArrangeItemAsync(project, WorkItemTypeId.Story, 5, sprint, false, null);
        await CompleteItemAsync(item, start.AddDays(2));
        await ReopenItemAsync(item, start.AddDays(5));

        Flags(await OutcomeAsync(sprint, item)).ShouldBe((true, false, false, true));
    }

    [Test]
    public async Task GetSprintItems_WhenDescopedMidSprint_ShouldNotBeCarried()
    {
        var (project, sprint, start, _) = await ArrangeSprintOnlyAsync();
        var item = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, sprint, false, null);
        await MoveItemAsync(item, null, start.AddDays(4));

        Flags(await OutcomeAsync(sprint, item)).ShouldBe((true, false, false, false));
    }

    [Test]
    public async Task GetSprintItems_WhenRemoved_ShouldNotBeCarried()
    {
        var (project, sprint, start, _) = await ArrangeSprintOnlyAsync();
        var item = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, sprint, false, null);
        await RemoveItemAsync(item, start.AddDays(3));

        Flags(await OutcomeAsync(sprint, item)).ShouldBe((true, false, false, false));
    }

    [Test]
    public async Task GetSprintItems_WhenMovedOutOnTheLastDay_ShouldBeCarried()
    {
        var (project, sprint, _, end) = await ArrangeSprintOnlyAsync();
        var item = await ArrangeItemAsync(project, WorkItemTypeId.Story, 3, sprint, false, null);
        await MoveItemAsync(item, null, end);

        Flags(await OutcomeAsync(sprint, item)).ShouldBe((true, false, false, true));
    }

    [Test]
    public async Task GetSprintItems_WhenMovedOnTheFridayBeforeASundayEnd_ShouldBeCarried()
    {
        var project = await ArrangeProjectAsync();
        var end = new DateOnly(2026, 10, 25);
        var start = end.AddDays(-13);
        var sprint = await ArrangeSprintAsync(project, "Weekend tail", start, end);
        var item = await ArrangeItemAsync(project, WorkItemTypeId.Story, 2, sprint, false, null);
        await MoveItemAsync(item, null, new DateOnly(2026, 10, 23));

        Flags(await OutcomeAsync(sprint, item)).ShouldBe((true, false, false, true));
    }

    [Test]
    public async Task GetSprintItems_WhenTaskOrBacklogItem_ShouldNotBeListed()
    {
        var (project, sprint, _, _) = await ArrangeSprintOnlyAsync();
        var task = await ArrangeItemAsync(project, WorkItemTypeId.Task, 1, sprint, false, null);
        var backlog = await ArrangeItemAsync(project, WorkItemTypeId.Story, 1, null, false, null);

        (await OutcomeAsync(sprint, task)).ShouldBeNull();
        (await OutcomeAsync(sprint, backlog)).ShouldBeNull();
    }

    [Test]
    public async Task GetSprintItems_ShouldListKeysInOrdinalOrder()
    {
        var (project, sprint, _, _) = await ArrangeSprintOnlyAsync();
        for (var i = 0; i < 11; i++)
        {
            await ArrangeItemAsync(project, WorkItemTypeId.Story, 1, sprint, false, null);
        }

        var keys = (await CreateMetrics().GetSprintItemsAsync(sprint)).Select(o => o.Key).ToList();

        keys.Count.ShouldBe(11);
        keys.ShouldBe([.. keys.Order(StringComparer.Ordinal)]);
    }
}
