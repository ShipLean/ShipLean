using System.Data.Common;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.States;

// Behaviour every IStateCatalog must have, whatever it stores data in.
[TestFixture]
public abstract class StateCatalogContract
{
    private static readonly WorkItemTypeId[] Types = [WorkItemTypeId.Epic, WorkItemTypeId.Feature, WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Task];

    protected abstract IStateCatalog CreateCatalog();

    // A project; with no states at all when withDefaultStates is false.
    protected abstract Task<Guid> ArrangeProjectAsync(bool withDefaultStates);

    protected abstract Task<DbTransaction> BeginTransactionAsync();

    protected abstract Task<Guid> ArrangeItemAsync(Guid projectId, Guid stateId);

    protected abstract Task<Guid> ReadItemStateAsync(Guid itemId);

    [Test]
    public async Task CreateDefaults_ShouldGiveEveryTypeAStatePerCategory()
    {
        var catalog = CreateCatalog();
        var project = await ArrangeProjectAsync(withDefaultStates: false);

        await catalog.CreateDefaultsAsync(project, await BeginTransactionAsync());

        var states = await catalog.ListAsync(project);
        foreach (var type in Types)
        {
            states.Where(s => s.Type == type).Select(s => s.Category).Distinct().Order().ShouldBe(Enum.GetValues<StateCategory>().Order());
        }
        states.Select(s => s.Type).Distinct().ShouldBe(Types);
    }

    [Test]
    public async Task List_WhenUnknownProject_ShouldBeNotFound()
    {
        var error = await Should.ThrowAsync<ApiException>(() => CreateCatalog().ListAsync(Guid.NewGuid()));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Copy_ShouldMapEveryStateAndKeepNamesAndCategories()
    {
        var catalog = CreateCatalog();
        var from = await ArrangeProjectAsync(withDefaultStates: true);
        var to = await ArrangeProjectAsync(withDefaultStates: false);

        var map = await catalog.CopyAsync(from, to, await BeginTransactionAsync());

        var source = await catalog.ListAsync(from);
        var copy = await catalog.ListAsync(to);
        map.Keys.OrderBy(k => k).ShouldBe(source.Select(s => s.Id).OrderBy(k => k));
        map.Values.OrderBy(v => v).ShouldBe(copy.Select(s => s.Id).OrderBy(v => v));
        foreach (var state in source)
        {
            var twin = copy.Single(s => s.Id == map[state.Id]);
            (twin.Type, twin.Name, twin.Category, twin.Order).ShouldBe((state.Type, state.Name, state.Category, state.Order));
        }
    }

    [Test]
    public async Task Apply_WhenRemovingTheLastCompletedState_ShouldBeValidation()
    {
        var catalog = CreateCatalog();
        var project = await ArrangeProjectAsync(withDefaultStates: true);
        var states = await catalog.ListAsync(project);
        var story = states.Where(s => s.Type == WorkItemTypeId.Story).ToList();
        var completed = story.Single(s => s.Category == StateCategory.Completed);
        var target = story.First(s => s.Category == StateCategory.Proposed);

        var error = await Should.ThrowAsync<ApiException>(
            async () => await catalog.ApplyAsync(project, new StateChanges([], [], [], [new StateRemove(completed.Id, target.Id)]), await BeginTransactionAsync()));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await catalog.ListAsync(project)).ShouldContain(s => s.Id == completed.Id);
    }

    [Test]
    public async Task Apply_WhenRemovingAStateWithItems_ShouldMoveTheItemsToTheTarget()
    {
        var catalog = CreateCatalog();
        var project = await ArrangeProjectAsync(withDefaultStates: true);
        var story = (await catalog.ListAsync(project)).Where(s => s.Type == WorkItemTypeId.Story).ToList();
        var active = story.Single(s => s.Category == StateCategory.InProgress);
        var target = story.First(s => s.Category == StateCategory.Proposed);
        var item = await ArrangeItemAsync(project, active.Id);

        await catalog.ApplyAsync(project, new StateChanges([], [], [], [new StateRemove(active.Id, target.Id)]), await BeginTransactionAsync());

        (await ReadItemStateAsync(item)).ShouldBe(target.Id);
        (await catalog.ListAsync(project)).Where(s => s.Type == WorkItemTypeId.Story).ShouldNotContain(s => s.Id == active.Id);
    }

    [Test]
    public async Task Apply_WhenAddedRenamedAndReordered_ShouldShowInTheList()
    {
        var catalog = CreateCatalog();
        var project = await ArrangeProjectAsync(withDefaultStates: true);
        var bug = (await catalog.ListAsync(project)).Where(s => s.Type == WorkItemTypeId.Bug).ToList();
        var renamed = bug.First(s => s.Category == StateCategory.Resolved);
        var order = bug.Select(s => s.Id).Reverse().ToList();

        await catalog.ApplyAsync(
            project,
            new StateChanges([new StateAdd(Guid.NewGuid(), WorkItemTypeId.Bug, "Triaged", StateCategory.Proposed)], [new StateUpdate(renamed.Id, "Fixed", StateCategory.Resolved)], [new StateReorder(WorkItemTypeId.Bug, order)], []),
            await BeginTransactionAsync());

        var after = (await catalog.ListAsync(project)).Where(s => s.Type == WorkItemTypeId.Bug).ToList();
        after.ShouldContain(s => s.Name == "Triaged" && s.Category == StateCategory.Proposed);
        after.Single(s => s.Id == renamed.Id).Name.ShouldBe("Fixed");
        after.Single(s => s.Id == renamed.Id).Category.ShouldBe(StateCategory.Resolved);
        after.Where(s => order.Contains(s.Id)).OrderBy(s => s.Order).Select(s => s.Id).ShouldBe(order);
    }

    [Test]
    public async Task Apply_WhenStateRecategorised_ShouldKeepItsIdAndItems()
    {
        var catalog = CreateCatalog();
        var project = await ArrangeProjectAsync(withDefaultStates: true);
        var bug = (await catalog.ListAsync(project)).Where(s => s.Type == WorkItemTypeId.Bug).ToList();
        var resolved = bug.First(s => s.Category == StateCategory.Resolved);
        var item = await ArrangeItemAsync(project, resolved.Id);

        await catalog.ApplyAsync(project, new StateChanges([], [new StateUpdate(resolved.Id, resolved.Name, StateCategory.InProgress)], [], []), await BeginTransactionAsync());

        var after = (await catalog.ListAsync(project)).Single(s => s.Id == resolved.Id);
        after.Category.ShouldBe(StateCategory.InProgress);
        after.ItemCount.ShouldBe(1);
        (await ReadItemStateAsync(item)).ShouldBe(resolved.Id);
    }

    [Test]
    public async Task Apply_WhenNewStateReordered_ShouldPlaceItWhereListed()
    {
        var catalog = CreateCatalog();
        var project = await ArrangeProjectAsync(withDefaultStates: true);
        var bug = (await catalog.ListAsync(project)).Where(s => s.Type == WorkItemTypeId.Bug).OrderBy(s => s.Order).ToList();
        var triaged = Guid.NewGuid();
        var order = new List<Guid> { bug[0].Id, triaged };
        order.AddRange(bug.Skip(1).Select(s => s.Id));

        await catalog.ApplyAsync(
            project,
            new StateChanges([new StateAdd(triaged, WorkItemTypeId.Bug, "Triaged", StateCategory.Proposed)], [], [new StateReorder(WorkItemTypeId.Bug, order)], []),
            await BeginTransactionAsync());

        var after = (await catalog.ListAsync(project)).Where(s => s.Type == WorkItemTypeId.Bug).OrderBy(s => s.Order).ToList();
        after.Select(s => s.Id).ShouldBe(order);
        after[1].Name.ShouldBe("Triaged");
        after[1].Category.ShouldBe(StateCategory.Proposed);
    }

    [Test]
    public async Task Apply_WhenRecategorisingTheLastCompletedState_ShouldBeValidation()
    {
        var catalog = CreateCatalog();
        var project = await ArrangeProjectAsync(withDefaultStates: true);
        var completed = (await catalog.ListAsync(project)).Single(s => s.Type == WorkItemTypeId.Story && s.Category == StateCategory.Completed);

        var error = await Should.ThrowAsync<ApiException>(
            async () => await catalog.ApplyAsync(project, new StateChanges([], [new StateUpdate(completed.Id, completed.Name, StateCategory.Resolved)], [], []), await BeginTransactionAsync()));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await catalog.ListAsync(project)).Single(s => s.Id == completed.Id).Category.ShouldBe(StateCategory.Completed);
    }
}
