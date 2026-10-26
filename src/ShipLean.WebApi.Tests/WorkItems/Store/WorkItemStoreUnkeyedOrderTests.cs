using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// Cards with no order key (created through the API, or seeded) are peers too: ordered `NULLS LAST, id`, and given keys when a drop lands among them.
[TestFixture]
public class WorkItemStoreUnkeyedOrderTests : PostgresWorkItemStoreTestBase
{
    private const string TASKS_IN_ORDER = "SELECT array_agg(id ORDER BY column_order COLLATE \"C\" NULLS LAST, id) FROM work_item WHERE parent_id = @p";

    [Test]
    public async Task Move_WhenATaskIsDroppedAboveAnUnkeyedTask_ShouldPlaceItThereAndKeyTheCell()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var parent = await CreateAsync(store, project, WorkItemTypeId.Story, "Parent");
        var first = await CreateAsync(store, project, WorkItemTypeId.Task, "First", parent.Id);
        var second = await CreateAsync(store, project, WorkItemTypeId.Task, "Second", parent.Id);
        var dropped = await CreateAsync(store, project, WorkItemTypeId.Task, "Dropped", parent.Id);
        (await QueryAsync<int>("SELECT count(column_order)::int FROM work_item WHERE parent_id = @p", new { p = parent.Id })).ShouldBe(0);
        var unplaced = await QueryAsync<Guid[]>("SELECT array_agg(id ORDER BY id) FROM work_item WHERE id IN (@a, @b)", new { a = first.Id, b = second.Id });

        Written(await store.MoveAsync(dropped.Id, new MoveTarget(1, null, false, null, unplaced[1], null), WriteOptions.User));

        (await QueryAsync<Guid[]>(TASKS_IN_ORDER, new { p = parent.Id })).ShouldBe([unplaced[0], dropped.Id, unplaced[1]]);
        (await QueryAsync<int>("SELECT count(DISTINCT column_order)::int FROM work_item WHERE parent_id = @p", new { p = parent.Id })).ShouldBe(3);
    }

    [Test]
    public async Task Move_WhenATaskIsDroppedLastInACellWithUnkeyedTasks_ShouldSortAfterThem()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var parent = await CreateAsync(store, project, WorkItemTypeId.Story, "Parent");
        var first = await CreateAsync(store, project, WorkItemTypeId.Task, "First", parent.Id);
        var second = await CreateAsync(store, project, WorkItemTypeId.Task, "Second", parent.Id);
        var dropped = await CreateAsync(store, project, WorkItemTypeId.Task, "Dropped", parent.Id);
        var unplaced = await QueryAsync<Guid[]>("SELECT array_agg(id ORDER BY id) FROM work_item WHERE id IN (@a, @b)", new { a = first.Id, b = second.Id });

        Written(await store.MoveAsync(dropped.Id, new MoveTarget(1, null, false, null, null, null), WriteOptions.User));

        (await QueryAsync<Guid[]>(TASKS_IN_ORDER, new { p = parent.Id })).ShouldBe([unplaced[0], unplaced[1], dropped.Id]);
    }

    [Test]
    public async Task Move_WhenACardIsDroppedLastInAColumnHalfWithUnkeyedCards_ShouldSortAfterThem()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var column = project.ColumnId(1);
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        Written(await store.MoveAsync(first.Id, new MoveTarget(1, column, false, null, null, null), WriteOptions.User));
        Written(await store.MoveAsync(second.Id, new MoveTarget(1, column, false, null, null, null), WriteOptions.User));
        await ExecuteAsync("UPDATE work_item SET column_order = NULL WHERE id IN (@a, @b)", new { a = first.Id, b = second.Id });
        var dropped = await CreateAsync(store, project, WorkItemTypeId.Story, "Dropped");
        var unplaced = await QueryAsync<Guid[]>("SELECT array_agg(id ORDER BY id) FROM work_item WHERE id IN (@a, @b)", new { a = first.Id, b = second.Id });

        Written(await store.MoveAsync(dropped.Id, new MoveTarget(1, column, false, null, null, null), WriteOptions.User));

        (await QueryAsync<Guid[]>(
            "SELECT array_agg(id ORDER BY column_order COLLATE \"C\" NULLS LAST, id) FROM work_item WHERE board_column_id = @column AND NOT board_column_done", new { column }))
            .ShouldBe([unplaced[0], unplaced[1], dropped.Id]);
    }
}
