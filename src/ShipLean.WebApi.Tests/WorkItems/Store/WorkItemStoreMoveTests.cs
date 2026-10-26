using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

[TestFixture]
public class WorkItemStoreMoveTests : PostgresWorkItemStoreTestBase
{
    [Test]
    public async Task Move_WhenColumnNotOnBoard_ShouldBeValidation()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Card");

        var error = await Should.ThrowAsync<ApiException>(
            () => store.MoveAsync(story.Id, new MoveTarget(1, "col-gone", false, null, null, null), WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await store.GetAsync(story.Id)).Revision.ShouldBe(1);
    }

    [Test]
    public async Task Move_WhenTaskboardDrop_ShouldOrderWithinCell()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var otherStory = await CreateAsync(store, project, WorkItemTypeId.Story, "Other story");
        var a = await CreateAsync(store, project, WorkItemTypeId.Task, "A", story.Id);
        var b = await CreateAsync(store, project, WorkItemTypeId.Task, "B", story.Id);
        var c = await CreateAsync(store, project, WorkItemTypeId.Task, "C", otherStory.Id);
        var active = new WorkItemChanges { State = new(project.StateId(WorkItemTypeId.Task, StateCategory.InProgress)) };
        Written(await store.MoveAsync(a.Id, new MoveTarget(1, null, false, null, null, active), WriteOptions.User));
        Written(await store.MoveAsync(c.Id, new MoveTarget(1, null, false, null, null, active), WriteOptions.User));

        Written(await store.MoveAsync(b.Id, new MoveTarget(1, null, false, null, a.Id, active), WriteOptions.User));
        var orderOfB = (await store.GetAsync(b.Id)).ColumnOrder.ShouldNotBeNull();
        var orderOfA = (await store.GetAsync(a.Id)).ColumnOrder.ShouldNotBeNull();
        var orderOfC = (await store.GetAsync(c.Id)).ColumnOrder.ShouldNotBeNull();

        orderOfA.ShouldBe("5");
        orderOfB.ShouldBe("3");
        orderOfC.ShouldBe("5");
        var error = await Should.ThrowAsync<ApiException>(
            () => store.MoveAsync(b.Id, new MoveTarget(2, null, false, null, c.Id, null), WriteOptions.User));
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Move_WhenDoneHalfGiven_ShouldOrderWithinThatHalf()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        var third = await CreateAsync(store, project, WorkItemTypeId.Story, "Third");
        Written(await store.MoveAsync(first.Id, new MoveTarget(1, project.ColumnId(1), true, null, null, null), WriteOptions.User));

        Written(await store.MoveAsync(second.Id, new MoveTarget(1, project.ColumnId(1), false, "lane-default", null, null), WriteOptions.User));
        Written(await store.MoveAsync(third.Id, new MoveTarget(1, project.ColumnId(1), true, null, first.Id, null), WriteOptions.User));

        var moved = await store.GetAsync(second.Id);
        moved.ColumnDone.ShouldBeFalse();
        moved.LaneId.ShouldBe("lane-default");
        (await store.GetAsync(first.Id)).ColumnDone.ShouldBeTrue();
        // The Doing half is empty when the second card lands, so it takes the first key although the Done half holds that key already.
        moved.ColumnOrder.ShouldBe("5");
        (await store.GetAsync(first.Id)).ColumnOrder.ShouldBe("5");
        (await store.GetAsync(third.Id)).ColumnOrder.ShouldBe("3");
    }

    [Test]
    public async Task Move_WhenStoryHasNoColumn_ShouldBeValidation()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Card");

        var error = await Should.ThrowAsync<ApiException>(() => store.MoveAsync(story.Id, new MoveTarget(1, null, false, null, null, null), WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await store.GetAsync(story.Id)).ColumnId.ShouldBe(project.ColumnId(0));
    }

    [Test]
    public async Task Move_WhenDoneGivenOnAColumnThatIsNotSplit_ShouldBeValidation()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Card");

        var error = await Should.ThrowAsync<ApiException>(() => store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(2), true, null, null, null), WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await store.GetAsync(story.Id)).ColumnDone.ShouldBeFalse();
    }

    [Test]
    public async Task Move_WhenLaneNotOnBoard_ShouldBeValidation()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Card");

        var error = await Should.ThrowAsync<ApiException>(
            () => store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, "lane-gone", null, null), WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Reorder_WhenPositionOnly_ShouldWriteNoRevision()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var a = await CreateAsync(store, project, WorkItemTypeId.Story, "A");
        var b = await CreateAsync(store, project, WorkItemTypeId.Story, "B");

        var result = Written(await store.ReorderAsync(b.Id, 1, a.Id));

        result.Revision.ShouldBe(1);
        (await QueryAsync<int>("SELECT count(*)::int FROM revision WHERE work_item_id = @id", new { id = b.Id })).ShouldBe(1);
        (await QueryAsync<int>("SELECT revision FROM work_item WHERE id = @id", new { id = b.Id })).ShouldBe(1);
        string.CompareOrdinal((await store.GetAsync(b.Id)).BacklogOrder, (await store.GetAsync(a.Id)).BacklogOrder).ShouldBeLessThan(0);
    }

    [Test]
    public async Task Reorder_WhenLast_ShouldGoAfterEveryItem()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var a = await CreateAsync(store, project, WorkItemTypeId.Story, "A");
        var b = await CreateAsync(store, project, WorkItemTypeId.Story, "B");

        Written(await store.ReorderAsync(b.Id, 1, null));

        string.CompareOrdinal((await store.GetAsync(b.Id)).BacklogOrder, (await store.GetAsync(a.Id)).BacklogOrder).ShouldBeGreaterThan(0);
    }

    [Test]
    public async Task Remove_ShouldMoveToRemovedStateAndKeepRow()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Doomed");

        var result = Written(await store.RemoveAsync(story.Id, 1));

        result.Revision.ShouldBe(2);
        result.Changes.Select(c => c.Field).ShouldBe(["state"]);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE id = @id", new { id = story.Id })).ShouldBe(1);
        (await QueryAsync<string>("SELECT s.category FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.id = @id", new { id = story.Id })).ShouldBe("removed");
    }
}
