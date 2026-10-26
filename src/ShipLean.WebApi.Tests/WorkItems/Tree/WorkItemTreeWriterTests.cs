using Dapper;
using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;
using ShipLean.WebApi.Tests.WorkItems.Store;

namespace ShipLean.WebApi.Tests.WorkItems.Tree;

// IWorkItemTreeWriter on Postgres, and the in-memory store held to the same outcome.
[TestFixture]
public class WorkItemTreeWriterTests : PostgresWorkItemStoreTestBase
{
    [Test]
    public async Task CreateTree_NewRoot_ShouldCreateAllKeysParentFirst()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();

        var outcome = await store.CreateTreeAsync(new WorkItemTreeCreate(project.ProjectId, null, [Plan()]), WriteOptions.User);

        var feature = outcome.Items.ShouldHaveSingleItem();
        Keys(outcome.Items).ShouldBe(Enumerable.Range(1, 7).Select(n => $"{project.Key}-{n}"));
        (await store.GetAsync(feature.Id)).ParentId.ShouldBeNull();
        foreach (var story in feature.Children)
        {
            (await store.GetAsync(story.Id)).ParentId.ShouldBe(feature.Id);
            foreach (var task in story.Children)
            {
                (await store.GetAsync(task.Id)).ParentId.ShouldBe(story.Id);
            }
        }

        feature.Children.Select(s => s.Children.Count).ShouldBe([2, 1, 0]);
        (await store.GetAsync(feature.Children[0].Children[1].Id)).Tags.ShouldBe(["model:fast"]);
        Arrangement.Events.Published.OfType<WorkItemCreated>().Where(e => e.ProjectId == project.ProjectId).Select(e => e.Key).ShouldBe(Keys(outcome.Items));
    }

    [Test]
    public async Task CreateTree_ExistingRoot_ShouldAddChildren()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Planning guide");

        var outcome = await store.CreateTreeAsync(new WorkItemTreeCreate(project.ProjectId, feature.Id, Plan().Children), WriteOptions.User);

        outcome.Items.Count.ShouldBe(3);
        Keys(outcome.Items).ShouldBe(Enumerable.Range(2, 6).Select(n => $"{project.Key}-{n}"));
        foreach (var story in outcome.Items)
        {
            (await store.GetAsync(story.Id)).ParentId.ShouldBe(feature.Id);
        }
    }

    [Test]
    public async Task CreateTree_Siblings_ShouldKeepInputOrderInBacklog()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var existing = await CreateAsync(store, project, WorkItemTypeId.Story, "Already there");

        var outcome = await store.CreateTreeAsync(new WorkItemTreeCreate(project.ProjectId, null, [Plan()]), WriteOptions.User);

        var backlog = (await BacklogAsync(project)).ToList();
        backlog.ShouldBe([.. Keys(outcome.Items), existing.Key]);
    }

    [Test]
    public async Task CreateTree_OneInvalidChild_ShouldCreateNothing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = Plan() with { Children = [.. Plan().Children, Node(WorkItemTypeId.Task, "A task can't sit under a Feature")] };

        var error = await Should.ThrowAsync<ApiException>(() => store.CreateTreeAsync(new WorkItemTreeCreate(project.ProjectId, null, [feature]), WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBe(0);
        Arrangement.Events.Published.Where(e => e.ProjectId == project.ProjectId).ShouldBeEmpty();
        (await CreateAsync(store, project, WorkItemTypeId.Story, "Next")).Key.ShouldBe($"{project.Key}-1");
    }

    [Test]
    public async Task CreateTree_UnknownRoot_ShouldBeNotFound()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();

        var error = await Should.ThrowAsync<ApiException>(() => store.CreateTreeAsync(new WorkItemTreeCreate(project.ProjectId, Guid.NewGuid(), Plan().Children), WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBe(0);
    }

    [Test]
    public async Task CreateTree_InMemory_ShouldMatchStore()
    {
        var project = await Arrangement.ArrangeProjectAsync();
        var memory = new InMemoryWorkItemStore(new PassThroughWorkItemRules(), new FixedCurrentUser(Actor), new MarkdownRenderer(), TimeProvider.System, AgentIdentity.None);
        memory.AddProject(project.ProjectId, project.Key, 1, project.States);
        IWorkItemStore[] stores = [PassThroughStore(), memory];
        var shapes = new List<IReadOnlyList<string>>();
        foreach (var store in stores)
        {
            var existing = WorkItemContractSupport.Written(await store.CreateAsync(new WorkItemCreate(project.ProjectId, WorkItemTypeId.Feature, "Planning guide", null, null, null), WriteOptions.User));
            var writer = (IWorkItemTreeWriter)store;
            await writer.CreateTreeAsync(new WorkItemTreeCreate(project.ProjectId, existing.Id, Plan().Children), WriteOptions.User);
            var outcome = await writer.CreateTreeAsync(new WorkItemTreeCreate(project.ProjectId, null, [Plan()]), WriteOptions.User);
            shapes.Add(await ShapeAsync(store, [existing.Id, .. Ids(outcome.Items)]));
        }

        shapes[1].ShouldBe(shapes[0]);
    }

    // Feature → Stories A (two tasks), B (one task), C (none): seven items, the second task of A tagged.
    private static WorkItemTreeCreate.Node Plan() => Node(
        WorkItemTypeId.Feature,
        "Planning guide",
        Node(WorkItemTypeId.Story, "A", Node(WorkItemTypeId.Task, "A1"), Node(WorkItemTypeId.Task, "A2") with { Changes = new() { Tags = new(["model:fast"]) } }),
        Node(WorkItemTypeId.Story, "B", Node(WorkItemTypeId.Task, "B1")),
        Node(WorkItemTypeId.Story, "C"));

    private static WorkItemTreeCreate.Node Node(WorkItemTypeId type, string title, params WorkItemTreeCreate.Node[] children) => new(type, title, null, children);

    // Parent-first, in input order: each item, then its children, then its next sibling.
    private static IEnumerable<string> Keys(IEnumerable<WorkItemTreeOutcome.Node> nodes) => nodes.SelectMany(n => Keys(n.Children).Prepend(n.Key));

    private static IEnumerable<Guid> Ids(IEnumerable<WorkItemTreeOutcome.Node> nodes) => nodes.SelectMany(n => Ids(n.Children).Prepend(n.Id));

    private async Task<IEnumerable<string>> BacklogAsync(ArrangedProject project)
    {
        await using var connection = await Arrangement.Database.OpenAsync();
        return await connection.QueryAsync<string>(
            "SELECT key FROM work_item WHERE project_id = @id ORDER BY backlog_order, id", new { id = project.ProjectId });
    }

    // Each item as "key type title parent-key tags", in backlog order.
    private static async Task<IReadOnlyList<string>> ShapeAsync(IWorkItemStore store, IEnumerable<Guid> ids)
    {
        var items = new List<WorkItemSnapshot>();
        foreach (var id in ids)
        {
            items.Add(await store.GetAsync(id));
        }

        var keys = items.ToDictionary(i => i.Id, i => i.Key);
        return [.. items
            .OrderBy(i => i.BacklogOrder, StringComparer.Ordinal)
            .Select(i => $"{i.Key} {i.Type} {i.Title} {(i.ParentId is { } parent ? keys[parent] : "-")} {string.Join(",", i.Tags)}")];
    }
}
