using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

[TestFixture]
public class WorkItemStoreOrderTests : PostgresWorkItemStoreTestBase
{
    [Test]
    public async Task Create_WhenManyAreAddedAtTheTopOfTheBacklog_ShouldKeepEveryKeyShortAndTheOrderIntact()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var created = new List<Guid>();

        for (var i = 0; i < 2000; i++)
        {
            created.Add((await CreateAsync(store, project, WorkItemTypeId.Story, $"Item {i}")).Id);
        }

        (await QueryAsync<int>("SELECT max(length(backlog_order))::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId }))
            .ShouldBeLessThanOrEqualTo(24);
        var inBacklogOrder = await QueryAsync<Guid[]>(
            "SELECT array_agg(id ORDER BY backlog_order COLLATE \"C\") FROM work_item WHERE project_id = @id", new { id = project.ProjectId });
        inBacklogOrder.ShouldBe([.. Enumerable.Reverse(created)]);
    }

    [Test]
    public async Task Read_WhenLinksAreAddedInOneWrite_ShouldGiveThemInTheOrderAdded()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        string[] urls = ["https://example.test/e", "https://example.test/b", "https://example.test/d", "https://example.test/a", "https://example.test/c", "https://example.test/f"];

        await PatchAsync(store, story.Id, 1, new WorkItemChanges { AddLinks = [.. urls.Select(u => new LinkValue(LinkType.Url, null, null, u, "Doc"))] });

        (await Arrangement.CreateReads().GetAsync(story.Id)).Links.Select(l => l.Url).ShouldBe(urls);
    }

    [Test]
    public async Task Read_WhenAnEarlierLinkIsRemovedAndAddedAgain_ShouldKeepItsPlace()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var first = new LinkValue(LinkType.Url, null, null, "https://example.test/first", "Doc");
        var second = new LinkValue(LinkType.Url, null, null, "https://example.test/second", "Doc");
        await PatchAsync(store, story.Id, 1, new WorkItemChanges { AddLinks = [first, second] });
        var firstId = (await Arrangement.CreateReads().GetAsync(story.Id)).Links[0].Id;
        await PatchAsync(store, story.Id, 2, new WorkItemChanges { RemoveLinkIds = [firstId] });

        await PatchAsync(store, story.Id, 3, new WorkItemChanges { AddLinks = [first] });

        (await Arrangement.CreateReads().GetAsync(story.Id)).Links.Select(l => l.Url).ShouldBe(["https://example.test/first", "https://example.test/second"]);
    }

    [Test]
    public async Task Patch_WhenATasksStateMovesItToAnotherCell_ShouldPlaceItLastThereSoADropBetweenWorks()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var parent = await CreateAsync(store, project, WorkItemTypeId.Story, "Parent");
        var first = await CreateAsync(store, project, WorkItemTypeId.Task, "First", parent.Id);
        var second = await CreateAsync(store, project, WorkItemTypeId.Task, "Second", parent.Id);
        var third = await CreateAsync(store, project, WorkItemTypeId.Task, "Third", parent.Id);
        var active = project.StateId(WorkItemTypeId.Task, StateCategory.InProgress);
        Written(await store.MoveAsync(first.Id, new MoveTarget(1, null, false, null, null, new WorkItemChanges { State = new(active) }), WriteOptions.User));
        Written(await store.MoveAsync(second.Id, new MoveTarget(1, null, false, null, null, null), WriteOptions.User));

        await PatchAsync(store, second.Id, 1, new WorkItemChanges { State = new(active) });

        string.CompareOrdinal((await store.GetAsync(second.Id)).ColumnOrder, (await store.GetAsync(first.Id)).ColumnOrder).ShouldBeGreaterThan(0);
        var dropped = await DropBetweenAsync(store, first.Id, second.Id, third.Id, active);

        dropped.Order.ShouldBe(dropped.Expected);
        dropped.Distinct.ShouldBe(3);
    }

    [Test]
    public async Task Patch_WhenATasksParentMovesItToAnotherCell_ShouldPlaceItLastThereSoADropBetweenWorks()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var parent = await CreateAsync(store, project, WorkItemTypeId.Story, "Parent");
        var otherParent = await CreateAsync(store, project, WorkItemTypeId.Story, "Other parent");
        var first = await CreateAsync(store, project, WorkItemTypeId.Task, "First", parent.Id);
        var second = await CreateAsync(store, project, WorkItemTypeId.Task, "Second", otherParent.Id);
        var third = await CreateAsync(store, project, WorkItemTypeId.Task, "Third", parent.Id);
        var active = project.StateId(WorkItemTypeId.Task, StateCategory.InProgress);
        var toActive = new WorkItemChanges { State = new(active) };
        Written(await store.MoveAsync(first.Id, new MoveTarget(1, null, false, null, null, toActive), WriteOptions.User));
        Written(await store.MoveAsync(second.Id, new MoveTarget(1, null, false, null, null, toActive), WriteOptions.User));

        await PatchAsync(store, second.Id, 2, new WorkItemChanges { Parent = new(parent.Id) });

        string.CompareOrdinal((await store.GetAsync(second.Id)).ColumnOrder, (await store.GetAsync(first.Id)).ColumnOrder).ShouldBeGreaterThan(0);
        var dropped = await DropBetweenAsync(store, first.Id, second.Id, third.Id, active);

        dropped.Order.ShouldBe(dropped.Expected);
        dropped.Distinct.ShouldBe(3);
    }

    [Test]
    public async Task Reorder_WhenTwoNeighboursTieOnTheirKey_ShouldRenumberTheBacklogInsteadOfFailing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var a = await CreateAsync(store, project, WorkItemTypeId.Story, "A");
        var b = await CreateAsync(store, project, WorkItemTypeId.Story, "B");
        var c = await CreateAsync(store, project, WorkItemTypeId.Story, "C");
        await ExecuteAsync("UPDATE work_item SET backlog_order = '5' WHERE id IN (@a, @b)", new { a = a.Id, b = b.Id });
        var second = await QueryAsync<Guid>("SELECT id FROM work_item WHERE id IN (@a, @b) ORDER BY backlog_order, id OFFSET 1 LIMIT 1", new { a = a.Id, b = b.Id });
        var first = second == a.Id ? b.Id : a.Id;

        Written(await store.ReorderAsync(c.Id, 1, second));

        (await QueryAsync<Guid[]>("SELECT array_agg(id ORDER BY backlog_order COLLATE \"C\", id) FROM work_item WHERE project_id = @id", new { id = project.ProjectId }))
            .ShouldBe([first, c.Id, second]);
        (await QueryAsync<int>("SELECT count(DISTINCT backlog_order)::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBe(3);
    }

    [Test]
    public async Task Move_WhenTwoNeighboursTieOnTheirCellKey_ShouldRenumberTheCellInsteadOfFailing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var parent = await CreateAsync(store, project, WorkItemTypeId.Story, "Parent");
        var first = await CreateAsync(store, project, WorkItemTypeId.Task, "First", parent.Id);
        var second = await CreateAsync(store, project, WorkItemTypeId.Task, "Second", parent.Id);
        var third = await CreateAsync(store, project, WorkItemTypeId.Task, "Third", parent.Id);
        var active = project.StateId(WorkItemTypeId.Task, StateCategory.InProgress);
        var toActive = new WorkItemChanges { State = new(active) };
        Written(await store.MoveAsync(first.Id, new MoveTarget(1, null, false, null, null, toActive), WriteOptions.User));
        Written(await store.MoveAsync(second.Id, new MoveTarget(1, null, false, null, null, toActive), WriteOptions.User));
        await ExecuteAsync("UPDATE work_item SET column_order = '5' WHERE id IN (@a, @b)", new { a = first.Id, b = second.Id });

        var dropped = await DropBetweenAsync(store, first.Id, second.Id, third.Id, active);

        dropped.Order.ShouldBe(dropped.Expected);
        dropped.Distinct.ShouldBe(3);
    }

    [Test]
    public async Task Move_WhenManyAreDroppedAtTheTopOfAColumnHalf_ShouldWriteTheRenumberedKeysAndLeaveOtherScopesAlone()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var column = project.ColumnId(1);
        var inDone = await CreateAsync(store, project, WorkItemTypeId.Story, "Done");
        Written(await store.MoveAsync(inDone.Id, new MoveTarget(1, column, true, null, null, null), WriteOptions.User));
        var bystander = await CreateAsync(store, project, WorkItemTypeId.Story, "Bystander");
        var bystanderKey = (await store.GetAsync(bystander.Id)).ColumnOrder;
        var dropped = new List<Guid>();
        Guid? top = null;

        for (var i = 0; i < 150; i++)
        {
            var card = await CreateAsync(store, project, WorkItemTypeId.Story, $"Card {i}");
            Written(await store.MoveAsync(card.Id, new MoveTarget(1, column, false, null, top, null), WriteOptions.User));
            top = card.Id;
            dropped.Add(card.Id);
        }

        (await QueryAsync<int>("SELECT max(length(column_order))::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBeLessThanOrEqualTo(24);
        (await QueryAsync<Guid[]>("SELECT array_agg(id ORDER BY column_order COLLATE \"C\") FROM work_item WHERE board_column_id = @column AND NOT board_column_done", new { column }))
            .ShouldBe([.. Enumerable.Reverse(dropped)]);
        (await store.GetAsync(inDone.Id)).ColumnOrder.ShouldBe("5");
        (await store.GetAsync(bystander.Id)).ColumnOrder.ShouldBe(bystanderKey);
    }

    [Test]
    public async Task Move_WhenManyTasksAreDroppedAtTheTopOfACell_ShouldWriteTheRenumberedKeys()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var parent = await CreateAsync(store, project, WorkItemTypeId.Story, "Parent");
        var dropped = new List<Guid>();
        Guid? top = null;

        for (var i = 0; i < 150; i++)
        {
            var task = await CreateAsync(store, project, WorkItemTypeId.Task, $"Task {i}", parent.Id);
            Written(await store.MoveAsync(task.Id, new MoveTarget(1, null, false, null, top, null), WriteOptions.User));
            top = task.Id;
            dropped.Add(task.Id);
        }

        (await QueryAsync<int>("SELECT max(length(column_order))::int FROM work_item WHERE parent_id = @p", new { p = parent.Id })).ShouldBeLessThanOrEqualTo(24);
        (await QueryAsync<Guid[]>("SELECT array_agg(id ORDER BY column_order COLLATE \"C\") FROM work_item WHERE parent_id = @p", new { p = parent.Id }))
            .ShouldBe([.. Enumerable.Reverse(dropped)]);
    }

    [Test]
    public async Task Move_WhenAFeatureAndAStoryShareAColumnId_ShouldOrderEachAmongItsOwnLevel()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await ExecuteAsync(
            "INSERT INTO board (id, project_id, level, settings) SELECT gen_random_uuid(), project_id, 'features', settings FROM board WHERE project_id = @id AND level = 'stories'",
            new { id = project.ProjectId });
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        Written(await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));

        Written(await store.MoveAsync(feature.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User));

        (await store.GetAsync(story.Id)).ColumnOrder.ShouldBe("5");
        (await store.GetAsync(feature.Id)).ColumnOrder.ShouldBe("5");
    }

    // Drops third before whichever of first and second sorts second in their cell, and returns the three in key order and how many distinct keys they hold.
    private async Task<(Guid[] Order, Guid[] Expected, int Distinct)> DropBetweenAsync(WorkItemStore store, Guid first, Guid second, Guid third, Guid active)
    {
        var sortedSecond = await QueryAsync<Guid>("SELECT id FROM work_item WHERE id IN (@a, @b) ORDER BY column_order COLLATE \"C\", id OFFSET 1 LIMIT 1", new { a = first, b = second });
        var sortedFirst = sortedSecond == first ? second : first;

        Written(await store.MoveAsync(third, new MoveTarget(1, null, false, null, sortedSecond, new WorkItemChanges { State = new(active) }), WriteOptions.User));

        var order = await QueryAsync<Guid[]>("SELECT array_agg(id ORDER BY column_order COLLATE \"C\", id) FROM work_item WHERE id IN (@a, @b, @c)", new { a = first, b = second, c = third });
        var distinct = await QueryAsync<int>("SELECT count(DISTINCT column_order)::int FROM work_item WHERE id IN (@a, @b, @c)", new { a = first, b = second, c = third });
        return (order, [sortedFirst, third, sortedSecond], distinct);
    }
}
