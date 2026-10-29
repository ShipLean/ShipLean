using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Features.WorkItems.Rules;
using System.Text.Json;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.WorkItems;
using ShipLean.WebApi.Tests.WorkItems.Store;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Rules;

[TestFixture]
public class WorkItemRulesTests : PostgresWorkItemStoreTestBase
{
    private WorkItemStore RealStore() => Arrangement.CreateStore(new WorkItemRules(TimeProvider.System), Actor);

    private static WorkItemChanges StateOf(ArrangedProject project, WorkItemTypeId type, StateCategory category)
        => new() { State = new(project.StateId(type, category)) };

    [Test]
    public async Task Rules_WhenUnassignedStoryMovedToInProgress_ShouldAssignActor()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var result = await PatchAsync(store, story.Id, 1, StateOf(project, WorkItemTypeId.Story, StateCategory.InProgress));

        (await store.GetAsync(story.Id)).AssigneeId.ShouldBe(Actor);
        result.Notices.ShouldContain("pulled");
    }

    [Test]
    public async Task Rules_WhenUnassignedTaskMovedToInProgress_ShouldAssignActor()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var task = await CreateAsync(store, project, WorkItemTypeId.Task, "Task", story.Id);

        Written(await store.MoveAsync(task.Id, new MoveTarget(1, null, false, null, null, StateOf(project, WorkItemTypeId.Task, StateCategory.InProgress)), WriteOptions.User));

        (await store.GetAsync(task.Id)).AssigneeId.ShouldBe(Actor);
    }

    [Test]
    public async Task Rules_WhenFeatureMovedToInProgress_ShouldNotAssign()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");

        var result = await PatchAsync(store, feature.Id, 1, StateOf(project, WorkItemTypeId.Feature, StateCategory.InProgress));

        (await store.GetAsync(feature.Id)).AssigneeId.ShouldBeNull();
        result.Notices.ShouldBeEmpty();
    }

    [Test]
    public async Task Rules_WhenStoryAlreadyAssigned_ShouldKeepAssignee()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var priya = SeedIds.Users.Priya;
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story", changes: new WorkItemChanges { Assignee = new(priya) });

        var result = await PatchAsync(store, story.Id, 1, StateOf(project, WorkItemTypeId.Story, StateCategory.InProgress));

        (await store.GetAsync(story.Id)).AssigneeId.ShouldBe(priya);
        result.Notices.ShouldNotContain("pulled");
    }

    [Test]
    public async Task Move_WhenColumnSet_ShouldSetMappedState()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var result = Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(2), false, null, null, null), WriteOptions.User));

        var moved = await store.GetAsync(story.Id);
        moved.StateId.ShouldBe(project.StateId(WorkItemTypeId.Story, StateCategory.Completed));
        moved.ColumnId.ShouldBe(project.ColumnId(2));
        result.Notices.ShouldContain("State set to Closed");
    }

    [Test]
    public async Task Move_WhenColumnAndInconsistentStateSet_ShouldLetTheColumnWin()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var result = Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(2), false, null, null, StateOf(project, WorkItemTypeId.Story, StateCategory.InProgress)), WriteOptions.User));

        var moved = await store.GetAsync(story.Id);
        moved.StateId.ShouldBe(project.StateId(WorkItemTypeId.Story, StateCategory.Completed));
        moved.ColumnId.ShouldBe(project.ColumnId(2));
        result.Notices.ShouldContain("Kept the column: state set to Closed");
    }

    [Test]
    public async Task Patch_WhenStateSet_ShouldMoveToFirstMappedColumn()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), true, null, null, null), WriteOptions.User));

        var result = await PatchAsync(store, story.Id, 2, StateOf(project, WorkItemTypeId.Story, StateCategory.Completed));

        var moved = await store.GetAsync(story.Id);
        moved.ColumnId.ShouldBe(project.ColumnId(2));
        moved.ColumnDone.ShouldBeFalse();
        moved.ColumnOrder.ShouldNotBeNull();
        result.Notices.ShouldContain("Moved to Done");
    }

    [Test]
    public async Task Patch_WhenStateMappedToNoColumn_ShouldLeaveTheBoardWithNotice()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var result = await PatchAsync(store, story.Id, 1, StateOf(project, WorkItemTypeId.Story, StateCategory.Removed));

        var gone = await store.GetAsync(story.Id);
        gone.ColumnId.ShouldBeNull();
        gone.ColumnOrder.ShouldBeNull();
        result.Notices.ShouldContain("Left the board: no column maps Removed");
    }

    [Test]
    public async Task Patch_WhenBugStateSet_ShouldMoveOnTheStoriesBoard()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var bug = await CreateAsync(store, project, WorkItemTypeId.Bug, "Bug");

        await PatchAsync(store, bug.Id, 1, StateOf(project, WorkItemTypeId.Bug, StateCategory.InProgress));

        var moved = await store.GetAsync(bug.Id);
        moved.ColumnId.ShouldBe(project.ColumnId(1));
        moved.AssigneeId.ShouldBe(Actor);
    }

    [Test]
    public async Task Move_WhenCardReorderingChangesBacklogOrder_ShouldPlaceCardByItsColumnNeighbour()
    {
        var (store, first, second) = await ArrangeTwoCardsAsync("changesBacklogOrder");

        var one = await store.GetAsync(first.Id);
        var two = await store.GetAsync(second.Id);

        // The second card was created above the first; it sits last in the column, so it now sits below the first in the backlog.
        string.CompareOrdinal(two.BacklogOrder, one.BacklogOrder).ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task Move_WhenCardReorderingKeepsBacklogOrder_ShouldLeaveBacklogOrderAlone()
    {
        var (store, first, second) = await ArrangeTwoCardsAsync("keepsBacklogOrder");

        var one = await store.GetAsync(first.Id);
        var two = await store.GetAsync(second.Id);

        string.CompareOrdinal(two.BacklogOrder, one.BacklogOrder).ShouldBeLessThan(0);
    }

    [Test]
    public async Task Move_WhenUnassignedStoryDroppedInInProgressColumn_ShouldAssignActor()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var result = Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));

        var moved = await store.GetAsync(story.Id);
        moved.AssigneeId.ShouldBe(Actor);
        moved.StateId.ShouldBe(project.StateId(WorkItemTypeId.Story, StateCategory.InProgress));
        result.Notices.ShouldContain("pulled");
    }

    [Test]
    public async Task Rules_WhenEpicMovedToInProgress_ShouldNotAssign()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var epic = await CreateAsync(store, project, WorkItemTypeId.Epic, "Epic");

        var result = await PatchAsync(store, epic.Id, 1, StateOf(project, WorkItemTypeId.Epic, StateCategory.InProgress));

        (await store.GetAsync(epic.Id)).AssigneeId.ShouldBeNull();
        result.Notices.ShouldNotContain("pulled");
    }

    [Test]
    public async Task Rules_WhenStoryMovesToInProgressFromCompleted_ShouldNotAssign()
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        await PatchAsync(store, story.Id, 1, StateOf(project, WorkItemTypeId.Story, StateCategory.Completed));

        var result = await PatchAsync(store, story.Id, 2, StateOf(project, WorkItemTypeId.Story, StateCategory.InProgress));

        (await store.GetAsync(story.Id)).AssigneeId.ShouldBeNull();
        result.Notices.ShouldNotContain("pulled");
    }

    [TestCase(WorkItemTypeId.Feature, "features")]
    [TestCase(WorkItemTypeId.Epic, "epics")]
    public async Task Patch_WhenFeatureOrEpicStateSet_ShouldMoveOnTheBoardOfItsLevel(WorkItemTypeId type, string level)
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var columnId = $"{level}-{Guid.NewGuid():N}";
        await ArrangeBoardAsync(project, level, columnId, type);
        var item = await CreateAsync(store, project, type, "Item");

        var result = await PatchAsync(store, item.Id, 1, StateOf(project, type, StateCategory.InProgress));

        (await store.GetAsync(item.Id)).ColumnId.ShouldBe(columnId);
        result.Notices.ShouldContain($"Moved to {level}");
    }

    [Test]
    public async Task Move_WhenDroppedOnAnotherCard_ShouldPlaceCardJustAboveItsNextNeighbour()
    {
        var (store, project, cards) = await ArrangeCardsAsync(3);
        var dropped = await CreateAsync(store, project, WorkItemTypeId.Story, "Dropped");

        // Cards sit A, B, C in the column and, as each was dropped below the last, in the backlog too; the new card goes between A and B in the column.
        Written(await store.MoveAsync(dropped.Id, new MoveTarget(1, project.ColumnId(1), false, null, cards[1], null), WriteOptions.User));

        var order = await BacklogOrderAsync(store, [cards[0], dropped.Id, cards[1], cards[2]]);
        order.ShouldBe(order.Order(StringComparer.Ordinal).ToArray());
    }

    [Test]
    public async Task Move_WhenCardDroppedWithinItsColumn_ShouldPlaceCardJustAboveItsNextNeighbour()
    {
        var (store, project, cards) = await ArrangeCardsAsync(3);

        // The last card in the column goes to the top of it, so it takes the backlog place just above the card that is now below it (A).
        Written(await store.MoveAsync(cards[2], new MoveTarget(2, project.ColumnId(1), false, null, cards[0], null), WriteOptions.User));

        var order = await BacklogOrderAsync(store, [cards[2], cards[0], cards[1]]);
        order.ShouldBe(order.Order(StringComparer.Ordinal).ToArray());
    }

    private async Task<(WorkItemStore Store, ArrangedProject Project, Guid[] Cards)> ArrangeCardsAsync(int count)
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await ExecuteAsync(
            "UPDATE board SET settings = jsonb_set(settings, '{cardReordering}', to_jsonb('changesBacklogOrder'::text)) WHERE project_id = @projectId AND level = 'stories'",
            new { projectId = project.ProjectId });
        var cards = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var card = await CreateAsync(store, project, WorkItemTypeId.Story, $"Card {i}");
            Written(await store.MoveAsync(card.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));
            cards.Add(card.Id);
        }

        return (store, project, [.. cards]);
    }

    // The backlog keys of the given items, in the order given.
    private static async Task<string[]> BacklogOrderAsync(IWorkItemStore store, Guid[] ids)
    {
        var keys = new List<string>();
        foreach (var id in ids)
        {
            keys.Add((await store.GetAsync(id)).BacklogOrder);
        }

        return [.. keys];
    }

    // A board at the given level with one column that maps the In Progress state of the type, named after the level.
    private async Task ArrangeBoardAsync(ArrangedProject project, string level, string columnId, WorkItemTypeId type)
    {
        BoardColumnJson[] columns = [new(columnId, level, null, false, "", [new(type, project.StateId(type, StateCategory.InProgress))])];
        var settings = new BoardSettingsJson(
            columns,
            [new SwimlaneJson("lane-default", "Everything else", null, SwimlaneKind.Default, null)],
            [],
            [],
            [],
            new CardAnnotationsJson(false, false, false, false),
            null,
            []);
        await ExecuteAsync(
            "INSERT INTO board (id, project_id, level, settings) VALUES (@id, @projectId, @level, @settings::jsonb)",
            new { id = Guid.NewGuid(), projectId = project.ProjectId, level, settings = JsonSerializer.Serialize(settings, PersistedJson.Options) });
    }

    private async Task<(WorkItemStore Store, WriteResult First, WriteResult Second)> ArrangeTwoCardsAsync(string reordering)
    {
        var store = RealStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await ExecuteAsync(
            "UPDATE board SET settings = jsonb_set(settings, '{cardReordering}', to_jsonb(@value::text)) WHERE project_id = @projectId AND level = 'stories'",
            new { value = reordering, projectId = project.ProjectId });
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        Written(await store.MoveAsync(first.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));
        Written(await store.MoveAsync(second.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));
        return (store, first, second);
    }
}
