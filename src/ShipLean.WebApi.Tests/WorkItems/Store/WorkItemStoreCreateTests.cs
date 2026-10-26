using System.Text.Json;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

[TestFixture]
public class WorkItemStoreCreateTests : PostgresWorkItemStoreTestBase
{
    [Test]
    public async Task Create_WhenStoryParentIsEpic_ShouldBeValidation()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var epic = await CreateAsync(store, project, WorkItemTypeId.Epic, "Epic");

        var error = await Should.ThrowAsync<ApiException>(() => CreateAsync(store, project, WorkItemTypeId.Story, "Story", epic.Id));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBe(1);
    }

    [Test]
    public async Task Create_WhenNoParent_ShouldSucceed()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();

        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Orphan");

        var stored = await store.GetAsync(story.Id);
        stored.ParentId.ShouldBeNull();
        stored.ParentType.ShouldBeNull();
        story.Key.ShouldBe($"{project.Key}-1");
        stored.StateId.ShouldBe(project.StateId(WorkItemTypeId.Story, StateCategory.Proposed));
    }

    [Test]
    public async Task Create_WhenParentIsAnotherProjectsItem_ShouldBeNotFound()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var other = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, other, WorkItemTypeId.Feature, "Elsewhere");

        var error = await Should.ThrowAsync<ApiException>(() => CreateAsync(store, project, WorkItemTypeId.Story, "Story", feature.Id));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Create_WhenProjectIsUnknown_ShouldBeNotFound()
    {
        var store = PassThroughStore();

        var error = await Should.ThrowAsync<ApiException>(
            () => store.CreateAsync(new WorkItemCreate(Guid.NewGuid(), WorkItemTypeId.Story, "Nowhere", null, null, null), WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Create_WhenTitleIsBlank_ShouldBeValidationAndKeepTheKeyNumber()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();

        var error = await Should.ThrowAsync<ApiException>(() => CreateAsync(store, project, WorkItemTypeId.Story, "  "));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await QueryAsync<int>("SELECT next_item_number FROM project WHERE id = @id", new { id = project.ProjectId })).ShouldBe(1);
    }

    [Test]
    public async Task Create_WhenStateMappedToColumn_ShouldPlaceInFirstMappedColumn()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await MapProposedAlsoInColumnAsync(project, 1);

        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Bug, "Second");
        var task = await CreateAsync(store, project, WorkItemTypeId.Task, "Task");

        var a = await store.GetAsync(first.Id);
        var b = await store.GetAsync(second.Id);
        a.ColumnId.ShouldBe(project.ColumnId(0));
        a.LaneId.ShouldBe("lane-default");
        a.ColumnDone.ShouldBeFalse();
        b.ColumnId.ShouldBe(project.ColumnId(0));
        string.CompareOrdinal(a.ColumnOrder.ShouldNotBeNull(), b.ColumnOrder.ShouldNotBeNull()).ShouldBeLessThan(0);
        var placedTask = await store.GetAsync(task.Id);
        placedTask.ColumnId.ShouldBeNull();
        placedTask.ColumnOrder.ShouldBeNull();
    }

    [Test]
    public async Task Create_ShouldPutTheNewItemAtTheTopOfTheBacklog()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();

        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Feature, "Second");

        string.CompareOrdinal((await store.GetAsync(second.Id)).BacklogOrder, (await store.GetAsync(first.Id)).BacklogOrder).ShouldBeLessThan(0);
    }

    [Test]
    public async Task Create_ShouldWriteRevisionOneWithTheFieldsSet()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var changes = new WorkItemChanges
        {
            Points = new(3m),
            Assignee = new(SeedIds.Users.Priya),
            Tags = new(["alpha"]),
            AddLinks = [new LinkValue(LinkType.Url, null, null, "https://example.test/spec", "Spec")],
        };

        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "With changes", null, changes);

        story.Revision.ShouldBe(1);
        story.Changes.Select(c => c.Field).ShouldBe(["title", "type", "state", "assignee", "priority", "points", "tags", "links"]);
        var revisions = await store.GetRevisionsAsync(story.Id);
        revisions.Count.ShouldBe(1);
        revisions[0].ChangedBy.ShouldBe(Actor);
        revisions[0].OriginalSystem.ShouldBeNull();
        var stored = await store.GetAsync(story.Id);
        stored.Links.Count.ShouldBe(1);
        stored.Links[0].Value.Url.ShouldBe("https://example.test/spec");
        revisions[0].Changes.Single(c => c.Field == "assignee").ToLabel.ShouldBe("Priya Shah");
    }

    [Test]
    public async Task Create_WhenConcurrent_ShouldNeverRepeatKey()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => CreateAsync(store, project, WorkItemTypeId.Story, $"Parallel {i}")));

        results.Select(r => r.Key).Distinct().Count().ShouldBe(20);
        (await QueryAsync<int>("SELECT next_item_number FROM project WHERE id = @id", new { id = project.ProjectId })).ShouldBe(21);
        (await QueryAsync<int>("SELECT count(DISTINCT backlog_order)::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBe(20);
    }

    // Column 1 also maps the Proposed states, after column 0.
    private async Task MapProposedAlsoInColumnAsync(ArrangedProject project, int column)
    {
        var settings = JsonSerializer.Deserialize<BoardSettingsJson>(
            await QueryAsync<string>("SELECT settings::text FROM board WHERE project_id = @id", new { id = project.ProjectId }), PersistedJson.Options)!;
        var columns = settings.Columns.ToList();
        columns[column] = columns[column] with
        {
            StateMappings =
            [
                new ColumnStateMapping(WorkItemTypeId.Story, project.StateId(WorkItemTypeId.Story, StateCategory.Proposed)),
                new ColumnStateMapping(WorkItemTypeId.Bug, project.StateId(WorkItemTypeId.Bug, StateCategory.Proposed)),
            ],
        };
        await ExecuteAsync(
            "UPDATE board SET settings = @settings::jsonb WHERE project_id = @id",
            new { id = project.ProjectId, settings = JsonSerializer.Serialize(settings with { Columns = columns }, PersistedJson.Options) });
    }
}
