using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

[TestFixture]
public class WorkItemStoreEventTests : PostgresWorkItemStoreTestBase
{
    [Test]
    public async Task Write_WhenCreated_ShouldPublishWorkItemCreated()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");

        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story", feature.Id);

        var created = EventsFor(story.Id).ShouldHaveSingleItem().ShouldBeOfType<WorkItemCreated>();
        created.ProjectId.ShouldBe(project.ProjectId);
        created.Key.ShouldBe(story.Key);
        created.WorkItemType.ShouldBe(WorkItemTypeId.Story);
        created.ParentId.ShouldBe(feature.Id);
    }

    [Test]
    public async Task Write_WhenPatched_ShouldPublishWorkItemUpdatedWithFields()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        await PatchAsync(store, story.Id, 1, new WorkItemChanges { Title = new("Renamed"), Points = new(2m) });

        var updated = EventsFor(story.Id)[^1].ShouldBeOfType<WorkItemUpdated>();
        updated.Revision.ShouldBe(2);
        updated.Fields.ShouldBe(["title", "points"]);
        updated.Pulled.ShouldBeFalse();
    }

    [Test]
    public async Task Write_WhenRulesPullTheItem_ShouldMarkTheEventPulled()
    {
        var store = Arrangement.CreateStore(new RecordingWorkItemRules(notices: ["pulled"]), Actor);
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        await PatchAsync(store, story.Id, 1, new WorkItemChanges { Points = new(2m) });

        EventsFor(story.Id)[^1].ShouldBeOfType<WorkItemUpdated>().Pulled.ShouldBeTrue();
    }

    [Test]
    public async Task Write_WhenConflict_ShouldPublishNothing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        await PatchAsync(store, story.Id, 1, new WorkItemChanges { Points = new(2m) });
        var before = EventsFor(story.Id).Count;

        var outcome = await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(3m) }), WriteOptions.User);

        outcome.Conflict.ShouldNotBeNull();
        EventsFor(story.Id).Count.ShouldBe(before);
    }

    [Test]
    public async Task Write_WhenNothingChanges_ShouldPublishNothing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var before = EventsFor(story.Id).Count;

        await PatchAsync(store, story.Id, 1, new WorkItemChanges { Title = new("Story") });

        EventsFor(story.Id).Count.ShouldBe(before);
    }

    [Test]
    public async Task Write_WhenInTransactionThrows_ShouldWriteAndPublishNothing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var before = EventsFor(story.Id).Count;
        var failing = new WriteOptions((_, _) => throw new InvalidOperationException("hook failed"), RevisionOrigin.User);

        await Should.ThrowAsync<InvalidOperationException>(
            () => store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Never") }), failing));
        await Should.ThrowAsync<InvalidOperationException>(
            () => store.CreateAsync(new WorkItemCreate(project.ProjectId, WorkItemTypeId.Story, "Never created", null, null, null), failing));

        var stored = await store.GetAsync(story.Id);
        stored.Title.ShouldBe("Story");
        stored.Revision.ShouldBe(1);
        (await store.GetRevisionsAsync(story.Id)).Count.ShouldBe(1);
        EventsFor(story.Id).Count.ShouldBe(before);
        Arrangement.Events.Published.OfType<WorkItemCreated>().ShouldNotContain(e => e.ProjectId == project.ProjectId && e.Key != story.Key);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBe(1);
        (await QueryAsync<int>("SELECT next_item_number FROM project WHERE id = @id", new { id = project.ProjectId })).ShouldBe(2);
    }

    [Test]
    public async Task Write_WhenPatchManySucceeds_ShouldPublishOneUpdatePerItem()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");

        await store.PatchManyAsync(
            [
                (first.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) })),
                (second.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(2m) })),
            ],
            WriteOptions.User);

        EventsFor(first.Id)[^1].ShouldBeOfType<WorkItemUpdated>().Fields.ShouldBe(["points"]);
        EventsFor(second.Id)[^1].ShouldBeOfType<WorkItemUpdated>().Fields.ShouldBe(["points"]);
    }

    [Test]
    public async Task Write_WhenMoved_ShouldPublishWorkItemMoved()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), true, "lane-default", null, new WorkItemChanges { Points = new(5m) }), WriteOptions.User);

        var moved = MovedFor(story.Id).ShouldHaveSingleItem();
        moved.ProjectId.ShouldBe(project.ProjectId);
        moved.Revision.ShouldBe(2);
        moved.ColumnId.ShouldBe(project.ColumnId(1));
        moved.Done.ShouldBeTrue();
        moved.LaneId.ShouldBe("lane-default");
        moved.ColumnOrder.ShouldNotBeNullOrEmpty();
        moved.StateId.ShouldBe(project.StateId(WorkItemTypeId.Story, StateCategory.Proposed));
        moved.ParentId.ShouldBeNull();
    }

    [Test]
    public async Task Write_WhenTaskboardDrop_ShouldPublishNullColumn()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var task = await CreateAsync(store, project, WorkItemTypeId.Task, "Task", story.Id);
        var active = project.StateId(WorkItemTypeId.Task, StateCategory.InProgress);

        await store.MoveAsync(task.Id, new MoveTarget(1, null, false, null, null, new WorkItemChanges { State = new(active) }), WriteOptions.User);

        var moved = MovedFor(task.Id).ShouldHaveSingleItem();
        moved.ColumnId.ShouldBeNull();
        moved.StateId.ShouldBe(active);
        moved.ParentId.ShouldBe(story.Id);
        moved.ColumnOrder.ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Write_WhenMoveIsStale_ShouldPublishNothing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        await PatchAsync(store, story.Id, 1, new WorkItemChanges { Points = new(2m) });

        var outcome = await store.MoveAsync(story.Id, new MoveTarget(1, project.ColumnId(1), false, null, null, null), WriteOptions.User);

        outcome.Conflict.ShouldNotBeNull();
        MovedFor(story.Id).ShouldBeEmpty();
    }

    [Test]
    public async Task Write_WhenReordered_ShouldPublishWorkItemReordered()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var a = await CreateAsync(store, project, WorkItemTypeId.Story, "A");
        var b = await CreateAsync(store, project, WorkItemTypeId.Story, "B");

        await store.ReorderAsync(b.Id, 1, a.Id);

        var reordered = Arrangement.Events.Published.OfType<WorkItemReordered>().Single(e => e.Id == b.Id);
        reordered.ProjectId.ShouldBe(project.ProjectId);
        reordered.BacklogOrder.ShouldBe((await store.GetAsync(b.Id)).BacklogOrder);
    }

    [Test]
    public async Task Write_WhenRemoved_ShouldPublishWorkItemRemoved()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story", feature.Id);

        await store.RemoveAsync(story.Id, 1);

        var removed = Arrangement.Events.Published.OfType<WorkItemRemoved>().Single(e => e.Id == story.Id);
        removed.ProjectId.ShouldBe(project.ProjectId);
        removed.ParentId.ShouldBe(feature.Id);
        EventsFor(story.Id)[^1].ShouldBeOfType<WorkItemUpdated>().Fields.ShouldBe(["state"]);
    }

    private List<WorkItemMoved> MovedFor(Guid id) => [.. Arrangement.Events.Published.OfType<WorkItemMoved>().Where(e => e.Id == id)];

    private List<ProjectEvent> EventsFor(Guid id) => [.. Arrangement.Events.Published.Where(e => e switch
    {
        WorkItemCreated created => created.Id == id,
        WorkItemUpdated updated => updated.Id == id,
        _ => false,
    })];
}
