using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// What the store does around the rules: whether it calls them, what it writes of their answer, and where the card lands after.
[TestFixture]
public class WorkItemStoreRulesTests : PostgresWorkItemStoreTestBase
{
    [Test]
    public async Task Remove_ShouldNotRunTheRules()
    {
        var rules = new RecordingWorkItemRules();
        var store = Arrangement.CreateStore(rules, Actor);
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Doomed");
        var calls = rules.Calls.Count;

        Written(await store.RemoveAsync(story.Id, 1));

        rules.Calls.Count.ShouldBe(calls);
    }

    [Test]
    public async Task Write_WhenRulesAmendTheTitle_ShouldStoreTheAmendedTitle()
    {
        var rules = new RecordingWorkItemRules(change => change with { Title = change.Title + " (amended)" });
        var store = Arrangement.CreateStore(rules, Actor);
        var project = await Arrangement.ArrangeProjectAsync();

        var created = await CreateAsync(store, project, WorkItemTypeId.Story, "Title");
        await PatchAsync(store, created.Id, 1, new WorkItemChanges { Points = new(2m) });

        (await store.GetAsync(created.Id)).Title.ShouldBe("Title (amended) (amended)");
        (await QueryAsync<string>("SELECT title FROM work_item WHERE id = @id", new { id = created.Id })).ShouldBe("Title (amended) (amended)");
    }

    [Test]
    public async Task Move_WhenRulesFlipTheDoneHalf_ShouldOrderInTheFinalHalf()
    {
        var flip = Guid.Empty;
        var rules = new RecordingWorkItemRules(change => change.WorkItemId == flip ? change with { ColumnDone = true } : change);
        var store = Arrangement.CreateStore(rules, Actor);
        var project = await Arrangement.ArrangeProjectAsync();
        var column = project.ColumnId(1);
        var done = await CreateAsync(store, project, WorkItemTypeId.Story, "Done");
        var dropped = await CreateAsync(store, project, WorkItemTypeId.Story, "Dropped");
        Written(await store.MoveAsync(done.Id, new MoveTarget(1, column, true, null, null, null), WriteOptions.User));
        flip = dropped.Id;

        Written(await store.MoveAsync(dropped.Id, new MoveTarget(1, column, false, null, null, null), WriteOptions.User));

        var stored = await store.GetAsync(dropped.Id);
        stored.ColumnDone.ShouldBeTrue();
        (await store.GetAsync(done.Id)).ColumnOrder.ShouldBe("5");
        stored.ColumnOrder.ShouldBe("8");
    }

    [Test]
    public async Task Move_WhenRulesTakeTheCardOffItsBoard_ShouldPublishWorkItemMovedWithNoColumnOrder()
    {
        var offBoard = string.Empty;
        var rules = new RecordingWorkItemRules(change => change.ColumnId == offBoard ? change with { ColumnId = null, LaneId = null } : change);
        var store = Arrangement.CreateStore(rules, Actor);
        var project = await Arrangement.ArrangeProjectAsync();
        offBoard = project.ColumnId(1);
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var created = Arrangement.Events.Published.Count;

        Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));

        var moved = Arrangement.Events.Published.Skip(created).OfType<WorkItemMoved>().ShouldHaveSingleItem();
        moved.Id.ShouldBe(story.Id);
        moved.ColumnId.ShouldBeNull();
        moved.ColumnOrder.ShouldBeNull();
        (await store.GetAsync(story.Id)).ColumnOrder.ShouldBeNull();
    }

    [Test]
    public async Task Create_ShouldReadTheBoardForShare()
    {
        var rules = new ProbingWorkItemRules(Arrangement.Database.ConnectionString);
        var store = Arrangement.CreateStore(rules, Actor);
        var project = await Arrangement.ArrangeProjectAsync();

        await CreateAsync(store, project, WorkItemTypeId.Story, "Placed on the board");

        rules.BoardWasLocked.ShouldBe(true);
    }

    [Test]
    public async Task Move_WhenRulesTakeATaskToAnotherCell_ShouldPlaceItLastInThatCell()
    {
        var active = Guid.Empty;
        var flip = Guid.Empty;
        var rules = new RecordingWorkItemRules(change => change.WorkItemId == flip ? change with { StateId = active } : change);
        var store = Arrangement.CreateStore(rules, Actor);
        var project = await Arrangement.ArrangeProjectAsync();
        active = project.StateId(WorkItemTypeId.Task, StateCategory.InProgress);
        var parent = await CreateAsync(store, project, WorkItemTypeId.Story, "Parent");
        var first = await CreateAsync(store, project, WorkItemTypeId.Task, "First", parent.Id);
        var second = await CreateAsync(store, project, WorkItemTypeId.Task, "Second", parent.Id);
        flip = second.Id;
        Written(await store.MoveAsync(first.Id, new MoveTarget(1, null, false, null, null, new WorkItemChanges { State = new(active) }), WriteOptions.User));

        Written(await store.MoveAsync(second.Id, new MoveTarget(1, null, false, null, null, null), WriteOptions.User));

        var inCell = await QueryAsync<Guid[]>("SELECT array_agg(id ORDER BY column_order COLLATE \"C\", id) FROM work_item WHERE parent_id = @p AND state_id = @s", new { p = parent.Id, s = active });
        inCell.ShouldBe([first.Id, second.Id]);
        (await store.GetAsync(second.Id)).ColumnOrder.ShouldNotBe((await store.GetAsync(first.Id)).ColumnOrder);
    }
}
