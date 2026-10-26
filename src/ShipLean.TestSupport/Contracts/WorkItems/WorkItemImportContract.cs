using System.Text.Json;
using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using Shouldly;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.TestSupport.Contracts.WorkItems;

// Behaviour every IWorkItemStore.ImportAsync must have. The users in the imported revisions are the seeded ones.
[TestFixture]
public abstract class WorkItemImportContract
{
    // CreateStore is called first in each test, then ArrangeProjectAsync (the fake arranges the store it was just given;
    // Postgres derivations arrange rows).
    protected abstract IWorkItemStore CreateStore(IWorkItemRules rules, Guid actorId);

    // Returns a new project (its own key) on every call, with its own states (one per category for each type), a board
    // holding the columns ArrangedProject.ColumnId names, and an import job of its own.
    protected abstract Task<ArrangedProject> ArrangeProjectAsync();

    private static readonly DateTimeOffset FirstChange = new(2026, 9, 28, 17, 45, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StateChanged = new(2026, 9, 29, 8, 30, 0, TimeSpan.Zero);

    [Test]
    public async Task Import_WhenKeyNumberGiven_ShouldKeepItAndAdvanceSequence()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var imported = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-1", 5000, "Imported", null), CancellationToken.None);

        imported.Created.ShouldBeTrue();
        imported.Key.ShouldBe($"{project.Key}-5000");
        var stored = await store.GetAsync(imported.Id);
        stored.Number.ShouldBe(5000);
        stored.StateChangedAt.ShouldBe(StateChanged);
        var next = await CreateAsync(store, project, WorkItemTypeId.Story, "After the import");
        (await store.GetAsync(next.Id)).Number.ShouldBe(5001);
    }

    [Test]
    public async Task Import_WhenRerun_ShouldUpdateByOriginalSystemId()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var first = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-2", 5100, "Crash", 2m), CancellationToken.None);

        var second = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-2", 5100, "Crash", 3m), CancellationToken.None);

        second.Created.ShouldBeFalse();
        second.Id.ShouldBe(first.Id);
        second.Key.ShouldBe(first.Key);
        (await store.GetAsync(first.Id)).Points.ShouldBe(3m);
    }

    [Test]
    public async Task Import_ShouldKeepEachRevisionsOriginAuthorAndTime()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var imported = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-3", 5200, "History", null), CancellationToken.None);

        imported.Revision.ShouldBe(2);
        var revisions = await store.GetRevisionsAsync(imported.Id);
        revisions.Select(r => r.Rev).ShouldBe([1, 2]);
        revisions.Select(r => r.ChangedBy).ShouldBe([SeedIds.Users.Sam, SeedIds.Users.Priya]);
        revisions.Select(r => r.ChangedAt).ShouldBe([FirstChange, FirstChange.AddDays(2)]);
        revisions.Select(r => r.OriginalSystem).ShouldAllBe(system => system == "jira");
        revisions[0].ImportJobId.ShouldBe(project.ImportJobId);
    }

    [Test]
    public async Task Import_WhenRerun_ShouldAppendOnlyNewRevisions()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var first = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-5", 5400, "Grows", 2m), CancellationToken.None);
        var jiraChange = new ImportedRevision(SeedIds.Users.Sam, FirstChange.AddDays(5), [Change("points", 2m, 3m)]);

        var again = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-5", 5400, "Grows", 3m, [jiraChange]), CancellationToken.None);
        var unchanged = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-5", 5400, "Grows", 3m, [jiraChange]), CancellationToken.None);
        var renamed = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-5", 5400, "Renamed", 3m, [jiraChange]), CancellationToken.None);

        first.Revision.ShouldBe(2);
        again.Revision.ShouldBe(3);
        unchanged.Revision.ShouldBe(3);
        renamed.Revision.ShouldBe(4);
        var revisions = await store.GetRevisionsAsync(first.Id);
        revisions.Select(r => r.Rev).ShouldBe([1, 2, 3, 4]);
        revisions[2].ChangedBy.ShouldBe(SeedIds.Users.Sam);
        revisions[2].ChangedAt.ShouldBe(FirstChange.AddDays(5));
        revisions[2].Changes.Select(c => c.Field).ShouldBe(["points"]);
        revisions[3].Changes.Select(c => c.Field).ShouldBe(["title"]);
        (await store.GetAsync(first.Id)).StateChangedAt.ShouldBe(StateChanged);
    }

    [Test]
    public async Task Import_WhenJiraHistoryStopsShortOfTheItem_ShouldEndHistoryAtTheItemsValue()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var first = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-6", 5500, "Short history", 2m), CancellationToken.None);
        var jiraChange = new ImportedRevision(SeedIds.Users.Sam, FirstChange.AddDays(5), [Change("points", 2m, 3m)]);

        await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-6", 5500, "Short history", 5m, [jiraChange]), CancellationToken.None);

        var last = (await store.GetRevisionsAsync(first.Id))[^1];
        var points = last.Changes.Single(c => c.Field == "points");
        points.To.GetDecimal().ShouldBe(5m);
    }

    [Test]
    public async Task Import_ShouldNotCallTheRules()
    {
        var store = CreateStore(new ThrowingWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();

        var imported = await store.ImportAsync(project.ProjectId, project.ImportJobId, Imported(project, "PLATFORM-4", 5300, "No rules", 1m), CancellationToken.None);

        (await store.GetAsync(imported.Id)).Title.ShouldBe("No rules");
    }

    private static ImportedWorkItem Imported(ArrangedProject project, string externalId, int keyNumber, string title, decimal? points)
        => Imported(project, externalId, keyNumber, title, points, []);

    private static ImportedWorkItem Imported(ArrangedProject project, string externalId, int keyNumber, string title, decimal? points, IReadOnlyList<ImportedRevision> later) => new(
        new OriginalSystemDto("jira", externalId, $"https://jira.example.com/browse/{externalId}"),
        keyNumber,
        WorkItemTypeId.Bug,
        title,
        "Imported from Jira.",
        [],
        "1. Open it",
        2,
        project.StateId(WorkItemTypeId.Bug, StateCategory.Proposed),
        SeedIds.Users.Sam,
        null,
        2,
        points,
        null,
        ["Imported"],
        [],
        SeedIds.Users.Sam,
        FirstChange,
        SeedIds.Users.Priya,
        later.Count > 0 ? later[^1].ChangedAt : FirstChange.AddDays(2),
        StateChanged,
        [
            new ImportedRevision(SeedIds.Users.Sam, FirstChange, [Change("title", null, title)]),
            new ImportedRevision(SeedIds.Users.Priya, FirstChange.AddDays(2), [Change("priority", 3, 2)]),
            .. later,
        ]);

    private static FieldChange Change<T>(string field, T? from, T? to)
        => new(field, JsonSerializer.SerializeToElement(from), JsonSerializer.SerializeToElement(to), null, null);
}
