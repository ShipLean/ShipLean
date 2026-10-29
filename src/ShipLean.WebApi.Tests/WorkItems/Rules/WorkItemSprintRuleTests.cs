using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Features.WorkItems.Rules;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;
using ShipLean.WebApi.Tests.WorkItems.Store;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Rules;

// A story or bug with no sprint that a person starts goes into the sprint running today (UTC).
[TestFixture]
public class WorkItemSprintRuleTests : PostgresWorkItemStoreTestBase
{
    private static readonly DateOnly _today = new(2026, 10, 29);

    private WorkItemStore RealStore() => Arrangement.CreateStore(new WorkItemRules(new FakeTimeProvider(new DateTimeOffset(2026, 10, 29, 23, 30, 0, TimeSpan.Zero))), Actor);

    private static WorkItemChanges StateOf(ArrangedProject project, WorkItemTypeId type, StateCategory category)
        => new() { State = new(project.StateId(type, category)) };

    private async Task<Guid> ArrangeSprintAsync(ArrangedProject project, string name, int startOffset, int endOffset)
    {
        var id = Guid.NewGuid();
        await ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @projectId, @name, @start, @end)",
            new { id, projectId = project.ProjectId, name, start = _today.AddDays(startOffset), end = _today.AddDays(endOffset) });
        return id;
    }

    [TestCase(WorkItemTypeId.Story)]
    [TestCase(WorkItemTypeId.Bug)]
    public async Task Patch_WhenSprintlessItemStarted_ShouldPutItInTheCurrentSprint(WorkItemTypeId type)
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var current = await ArrangeSprintAsync(project, "Current", -3, 3);
        await ArrangeSprintAsync(project, "Later", 4, 10);
        var item = await CreateAsync(store, project, type, "Item");

        var result = await PatchAsync(store, item.Id, 1, StateOf(project, type, StateCategory.InProgress));

        (await store.GetAsync(item.Id)).SprintId.ShouldBe(current);
        result.Notices.ShouldContain("sprinted");
    }

    [Test]
    public async Task Move_WhenCardDraggedIntoAnInProgressColumn_ShouldPutItInTheCurrentSprint()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var current = await ArrangeSprintAsync(project, "Current", -3, 3);
        var item = await CreateAsync(store, project, WorkItemTypeId.Story, "Item");

        var result = Written(await store.MoveAsync(item.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));

        (await store.GetAsync(item.Id)).SprintId.ShouldBe(current);
        result.Notices.ShouldContain("sprinted");
    }

    [Test]
    public async Task Patch_WhenItemHasAPastSprint_ShouldKeepIt()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var past = await ArrangeSprintAsync(project, "Past", -20, -10);
        await ArrangeSprintAsync(project, "Current", -3, 3);
        var item = await CreateAsync(store, project, WorkItemTypeId.Story, "Item", changes: new WorkItemChanges { Sprint = new(past) });

        var result = await PatchAsync(store, item.Id, 1, StateOf(project, WorkItemTypeId.Story, StateCategory.InProgress));

        (await store.GetAsync(item.Id)).SprintId.ShouldBe(past);
        result.Notices.ShouldNotContain("sprinted");
    }

    [Test]
    public async Task Patch_WhenNoSprintIsCurrent_ShouldLeaveTheSprintEmpty()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await ArrangeSprintAsync(project, "Past", -20, -10);
        await ArrangeSprintAsync(project, "Later", 4, 10);
        var item = await CreateAsync(store, project, WorkItemTypeId.Story, "Item");

        var result = await PatchAsync(store, item.Id, 1, StateOf(project, WorkItemTypeId.Story, StateCategory.InProgress));

        (await store.GetAsync(item.Id)).SprintId.ShouldBeNull();
        result.Notices.ShouldNotContain("sprinted");
    }

    [Test]
    public async Task Patch_WhenFeatureOrTaskStarted_ShouldLeaveTheSprintEmpty()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await ArrangeSprintAsync(project, "Current", -3, 3);
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var task = await CreateAsync(store, project, WorkItemTypeId.Task, "Task", story.Id);

        await PatchAsync(store, feature.Id, 1, StateOf(project, WorkItemTypeId.Feature, StateCategory.InProgress));
        await PatchAsync(store, task.Id, 1, StateOf(project, WorkItemTypeId.Task, StateCategory.InProgress));

        (await store.GetAsync(feature.Id)).SprintId.ShouldBeNull();
        (await store.GetAsync(task.Id)).SprintId.ShouldBeNull();
    }

    [Test]
    public async Task Patch_WhenItemAlreadyInProgressAndIsEdited_ShouldNotTakeASprint()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var item = await CreateAsync(store, project, WorkItemTypeId.Story, "Item");
        await PatchAsync(store, item.Id, 1, StateOf(project, WorkItemTypeId.Story, StateCategory.InProgress));
        await ArrangeSprintAsync(project, "Current", -3, 3);

        var result = await PatchAsync(store, item.Id, 2, new WorkItemChanges { Title = new("Renamed") });

        (await store.GetAsync(item.Id)).SprintId.ShouldBeNull();
        result.Notices.ShouldNotContain("sprinted");
    }

    [Test]
    public async Task Patch_WhenWebhookStartsTheItem_ShouldNotTakeASprint()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await ArrangeSprintAsync(project, "Current", -3, 3);
        var item = await CreateAsync(store, project, WorkItemTypeId.Story, "Item");

        var outcome = await store.PatchAsync(item.Id, new WorkItemPatch(1, StateOf(project, WorkItemTypeId.Story, StateCategory.InProgress)), WriteOptions.FromWebhook("GitHub acme/api#12"));

        Written(outcome).Notices.ShouldNotContain("sprinted");
        (await store.GetAsync(item.Id)).SprintId.ShouldBeNull();
    }

    [Test]
    public async Task PatchMany_WhenBulkWriteMovesTheItemIntoProgress_ShouldNotTakeASprint()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await ArrangeSprintAsync(project, "Current", -3, 3);
        var item = await CreateAsync(store, project, WorkItemTypeId.Story, "Item");

        var outcome = await store.PatchManyAsync(
            [(item.Id, new WorkItemPatch(1, StateOf(project, WorkItemTypeId.Story, StateCategory.InProgress)))], WriteOptions.User with { Bulk = true });

        outcome.Conflicts.ShouldBeNull();
        outcome.Written.ShouldNotBeNull().Single().Notices.ShouldNotContain("sprinted");
        (await store.GetAsync(item.Id)).SprintId.ShouldBeNull();
    }
}
