using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

[TestFixture]
public class WorkItemStoreReadTests : PostgresWorkItemStoreTestBase
{
    [Test]
    public async Task Read_WhenItemHasChildren_ShouldSummariseThem()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First", feature.Id, new WorkItemChanges { Points = new(3m), Assignee = new(SeedIds.Users.Priya) });
        var second = await CreateAsync(store, project, WorkItemTypeId.Bug, "Second", feature.Id);
        var gone = await CreateAsync(store, project, WorkItemTypeId.Story, "Gone", feature.Id);
        await PatchAsync(store, gone.Id, 1, new WorkItemChanges { State = new(project.StateId(WorkItemTypeId.Story, StateCategory.Removed)) });

        var dto = await Arrangement.CreateReads().GetAsync(feature.Id);

        dto.Children.Select(c => c.Id).ShouldBe([gone.Id, second.Id, first.Id]);
        dto.Children.Single(c => c.Id == gone.Id).State.Category.ShouldBe(StateCategory.Removed);
        var child = dto.Children.Single(c => c.Id == first.Id);
        child.Key.ShouldBe(first.Key);
        child.Type.ShouldBe(new TypeRef(WorkItemTypeId.Story, "User Story"));
        child.State.Name.ShouldBe("New");
        child.State.Category.ShouldBe(StateCategory.Proposed);
        child.Assignee.ShouldNotBeNull().Name.ShouldBe("Priya Shah");
        child.Points.ShouldBe(3m);
        var story = await Arrangement.CreateReads().GetAsync(first.Id);
        story.Parent.ShouldNotBeNull().Key.ShouldBe(feature.Key);
        story.Parent.Type.Id.ShouldBe(WorkItemTypeId.Feature);
    }

    [Test]
    public async Task Read_ShouldBuildTheFullDto()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var other = await CreateAsync(store, project, WorkItemTypeId.Story, "Other");
        var changes = new WorkItemChanges
        {
            Description = new("**bold**"),
            Tags = new(["a", "b"]),
            Priority = new(1),
            AddLinks = [new LinkValue(LinkType.Related, other.Id, null, null, null), new LinkValue(LinkType.Url, null, null, "https://example.test", "Doc")],
        };
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Full", null, changes);

        var dto = await Arrangement.CreateReads().GetAsync(story.Id);

        dto.ProjectId.ShouldBe(project.ProjectId);
        dto.Key.ShouldBe(story.Key);
        dto.Type.Name.ShouldBe("User Story");
        dto.DescriptionHtml.ShouldContain("<strong>bold</strong>");
        dto.Tags.ShouldBe(["a", "b"]);
        dto.Priority.ShouldBe(1);
        dto.Revision.ShouldBe(1);
        dto.CreatedBy.ShouldNotBeNull().Id.ShouldBe(Actor);
        dto.CreatedAtLabel.ShouldBe(DisplayDate.Relative(dto.CreatedAt, TimeProvider.System));
        dto.Position.ColumnId.ShouldBe(project.ColumnId(0));
        dto.OriginalSystem.ShouldBeNull();
        dto.Links.Select(l => (l.Key, l.Url, l.Title)).ShouldBe([(other.Key, null, "Other"), (null, "https://example.test", "Doc")]);
    }

    [Test]
    public async Task Read_WhenItemIsUnknownOrInAnotherProject_ShouldBeNotFound()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var other = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var reads = Arrangement.CreateReads();

        (await Should.ThrowAsync<ApiException>(() => reads.GetAsync(Guid.NewGuid()))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => reads.GetByKeyAsync(project.ProjectId, "NOPE-1"))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => reads.GetByKeyAsync(other.ProjectId, story.Key))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await reads.GetByKeyAsync(project.ProjectId, story.Key.ToLowerInvariant())).Id.ShouldBe(story.Id);
    }

    [Test]
    public async Task List_ShouldGiveBacklogOrderWithoutRemovedItemsAndFilterByType()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Bug, "Second");
        var removed = await CreateAsync(store, project, WorkItemTypeId.Story, "Removed");
        await PatchAsync(store, removed.Id, 1, new WorkItemChanges { State = new(project.StateId(WorkItemTypeId.Story, StateCategory.Removed)) });
        var reads = Arrangement.CreateReads();

        (await reads.ListAsync(project.ProjectId, null)).Select(i => i.Id).ShouldBe([second.Id, first.Id]);
        (await reads.ListAsync(project.ProjectId, WorkItemTypeId.Story)).Select(i => i.Id).ShouldBe([first.Id]);
    }

    [Test]
    public async Task Read_WhenRevisionsAreRead_ShouldBeOldestFirst()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        await PatchAsync(store, story.Id, 1, new WorkItemChanges { Points = new(1m) });
        await PatchAsync(store, story.Id, 2, new WorkItemChanges { Points = new(2m) });

        (await store.GetRevisionsAsync(story.Id)).Select(r => r.Rev).ShouldBe([1, 2, 3]);
        (await Should.ThrowAsync<ApiException>(() => store.GetRevisionsAsync(Guid.NewGuid()))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => store.GetAsync(Guid.NewGuid()))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
