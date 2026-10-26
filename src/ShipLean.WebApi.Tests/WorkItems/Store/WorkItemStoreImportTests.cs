using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

[TestFixture]
public class WorkItemStoreImportTests : PostgresWorkItemStoreTestBase
{
    private static readonly DateTimeOffset At = new(2026, 9, 28, 17, 45, 0, TimeSpan.Zero);

    [Test]
    public async Task Import_WhenKeyBelowSequence_ShouldNotLowerIt()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-1", 5000, 2), CancellationToken.None);

        var low = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-2", 10, 2), CancellationToken.None);

        low.Key.ShouldBe($"{project.Key}-10");
        var next = await CreateAsync(store, project, WorkItemTypeId.Story, "After");
        (await store.GetAsync(next.Id)).Number.ShouldBe(5001);
    }

    [Test]
    public async Task Import_WhenKeyNumberBelongsToAnotherItem_ShouldBeConflict()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-1", 7, 2), CancellationToken.None);

        var error = await Should.ThrowAsync<ShipLean.Contracts.Errors.ApiException>(
            () => store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-2", 7, 2), CancellationToken.None));

        error.Code.ShouldBe(ShipLean.Contracts.Errors.ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Import_ShouldPublishNoRuleNotices()
    {
        var rules = new RecordingWorkItemRules(notices: ["pulled"]);
        var store = Arrangement.CreateStore(rules, Actor);
        var project = await Arrangement.ArrangeProjectAsync();

        await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-1", 6000, 2), CancellationToken.None);
        await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-1", 6000, 3), CancellationToken.None);

        rules.Calls.ShouldBeEmpty();
        var events = Arrangement.Events.Published.Where(e => e.ProjectId == project.ProjectId).ToList();
        events.Select(e => e.GetType()).ShouldBe([typeof(WorkItemCreated), typeof(WorkItemUpdated)]);
        events.OfType<WorkItemUpdated>().Single().Pulled.ShouldBeFalse();
    }

    [Test]
    public async Task Import_WhenRerunChangesState_ShouldMoveToMappedColumn()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-1", 100, 2), CancellationToken.None);
        (await store.GetAsync(first.Id)).ColumnId.ShouldBe(project.ColumnId(0));

        var completed = Imported(project, "PLATFORM-1", 100, 2) with { StateId = project.StateId(WorkItemTypeId.Bug, StateCategory.Completed) };
        await store.ImportAsync(project.ProjectId, project.ImportJobId, completed, CancellationToken.None);

        var item = await store.GetAsync(first.Id);
        item.ColumnId.ShouldBe(project.ColumnId(2));
        item.ColumnOrder.ShouldNotBeNull();
        var moved = Arrangement.Events.Published.OfType<WorkItemMoved>().Single(e => e.ProjectId == project.ProjectId);
        moved.Id.ShouldBe(first.Id);
        moved.ColumnId.ShouldBe(project.ColumnId(2));
    }

    [Test]
    public async Task Import_WhenRerunKeepsStateMappedByTheColumn_ShouldNotMove()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-1", 100, 2), CancellationToken.None);

        await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-1", 100, 3), CancellationToken.None);

        (await store.GetAsync(first.Id)).ColumnId.ShouldBe(project.ColumnId(0));
        Arrangement.Events.Published.OfType<WorkItemMoved>().Where(e => e.ProjectId == project.ProjectId).ShouldBeEmpty();
    }

    [Test]
    public async Task Import_WhenRerunMovesTaskToAnotherParent_ShouldLeaveTheOldCell()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var one = await CreateAsync(store, project, WorkItemTypeId.Story, "One");
        var two = await CreateAsync(store, project, WorkItemTypeId.Story, "Two");
        var task = Imported(project, "PLATFORM-1", 100, 2) with
        {
            Type = WorkItemTypeId.Task,
            StateId = project.StateId(WorkItemTypeId.Task, StateCategory.Proposed),
            ParentId = one.Id,
        };
        var first = await store.ImportAsync(project.ProjectId, project.ImportJobId, task, CancellationToken.None);
        await ExecuteAsync("UPDATE work_item SET column_order = '000000000500' WHERE id = @id", new { id = first.Id });

        await store.ImportAsync(project.ProjectId, project.ImportJobId, task with { ParentId = two.Id }, CancellationToken.None);

        (await store.GetAsync(first.Id)).ColumnOrder.ShouldBeNull();
        Arrangement.Events.Published.OfType<WorkItemMoved>().Single(e => e.ProjectId == project.ProjectId).ParentId.ShouldBe(two.Id);
    }

    [Test]
    public async Task Import_WhenRunConcurrently_ShouldCreateOnce()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(
            _ => store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-1", 100, 2), CancellationToken.None)));

        results.Count(r => r.Created).ShouldBe(1);
        results.Select(r => r.Id).Distinct().Count().ShouldBe(1);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBe(1);
    }

    private static ImportedWorkItem Imported(ArrangedProject project, string externalId, int keyNumber, int priority) => new(
        new OriginalSystemDto("jira", externalId, $"https://jira.example.com/browse/{externalId}"),
        keyNumber,
        WorkItemTypeId.Bug,
        "Imported",
        "",
        [],
        "",
        null,
        project.StateId(WorkItemTypeId.Bug, StateCategory.Proposed),
        null,
        null,
        priority,
        null,
        null,
        [],
        [],
        SeedIds.Users.Sam,
        At,
        SeedIds.Users.Sam,
        At,
        At,
        [new ImportedRevision(SeedIds.Users.Sam, At, [])]);
}
