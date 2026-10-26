using System.Data.Common;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

[TestFixture]
public class WorkItemStorePatchTests : PostgresWorkItemStoreTestBase
{
    [Test]
    public async Task Patch_ShouldWriteOneRevisionWithTheChangedFieldsOnly()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Before");

        var result = await PatchAsync(store, story.Id, 1, new WorkItemChanges { Title = new("After"), Points = new(5m), Description = new("Same"), Priority = new(2) });

        result.Revision.ShouldBe(2);
        result.Changes.Select(c => c.Field).ShouldBe(["title", "points", "description"]);
        var stored = await store.GetAsync(story.Id);
        stored.Revision.ShouldBe(2);
        stored.ChangedBy.ShouldBe(Actor);
        stored.Points.ShouldBe(5m);
        (await store.GetRevisionsAsync(story.Id)).Select(r => r.Rev).ShouldBe([1, 2]);
    }

    [Test]
    public async Task Patch_WhenStale_ShouldReturnConflictWithTheirChanges()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Original");
        await PatchAsync(store, story.Id, 1, new WorkItemChanges { Title = new("Theirs") });
        await PatchAsync(store, story.Id, 2, new WorkItemChanges { Points = new(8m) });

        var outcome = await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Mine") }), WriteOptions.User);

        outcome.Written.ShouldBeNull();
        var conflict = outcome.Conflict.ShouldNotBeNull();
        conflict.Code.ShouldBe(ApiErrorCodes.Conflict);
        conflict.Current.Title.ShouldBe("Theirs");
        conflict.Current.Revision.ShouldBe(3);
        conflict.Current.Points.ShouldBe(8m);
        conflict.TheirChanges.Select(r => r.Rev).ShouldBe([3, 2]);
        conflict.TheirChanges[0].ChangedBy.ShouldNotBeNull().Id.ShouldBe(Actor);
        conflict.TheirChanges[1].Changes.Select(c => c.Field).ShouldBe(["title"]);
        conflict.TheirChanges[1].Origin.ShouldBe(RevisionOrigin.User);
        (await store.GetAsync(story.Id)).Title.ShouldBe("Theirs");
    }

    [Test]
    public async Task Patch_WhenNothingChanges_ShouldWriteNoRevision()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Same", null, new WorkItemChanges { Points = new(2m) });

        var result = await PatchAsync(store, story.Id, 1, new WorkItemChanges { Title = new("Same"), Points = new(2.0m), Tags = new([]) });

        result.Revision.ShouldBe(1);
        result.Changes.ShouldBeEmpty();
        var stored = await store.GetAsync(story.Id);
        stored.Revision.ShouldBe(1);
        (await store.GetRevisionsAsync(story.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task Patch_WhenLinkRemovedAndReAdded_ShouldReviveTheRow()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Source");
        var other = await CreateAsync(store, project, WorkItemTypeId.Story, "Target");
        var link = new LinkValue(LinkType.BlockedBy, other.Id, null, null, null);
        await PatchAsync(store, story.Id, 1, new WorkItemChanges { AddLinks = [link] });
        var linkId = (await store.GetAsync(story.Id)).Links.Single().Id;

        var removed = await PatchAsync(store, story.Id, 2, new WorkItemChanges { RemoveLinkIds = [linkId] });
        var archived = await QueryAsync<bool>("SELECT archived_at IS NOT NULL FROM work_item_link WHERE id = @linkId", new { linkId });
        var readded = await PatchAsync(store, story.Id, 3, new WorkItemChanges { AddLinks = [link] });

        removed.Changes.Select(c => c.Field).ShouldBe(["links"]);
        archived.ShouldBeTrue();
        readded.Changes.Select(c => c.Field).ShouldBe(["links"]);
        (await store.GetAsync(story.Id)).Links.Single().Id.ShouldBe(linkId);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item_link WHERE source_id = @id", new { id = story.Id })).ShouldBe(1);
        (await QueryAsync<bool>("SELECT archived_at IS NULL FROM work_item_link WHERE id = @linkId", new { linkId })).ShouldBeTrue();
    }

    [Test]
    public async Task Patch_WhenLinkIsAlreadyThere_ShouldChangeNothing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Source");
        var link = new LinkValue(LinkType.Url, null, null, "https://example.test/a", "A");
        await PatchAsync(store, story.Id, 1, new WorkItemChanges { AddLinks = [link] });

        var result = await PatchAsync(store, story.Id, 2, new WorkItemChanges { AddLinks = [link] });

        result.Changes.ShouldBeEmpty();
        (await store.GetAsync(story.Id)).Links.Count.ShouldBe(1);
    }

    [Test]
    public async Task Patch_WhenLinkTargetIsInAnotherProject_ShouldBeNotFound()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var other = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Source");
        var foreign = await CreateAsync(store, other, WorkItemTypeId.Story, "Foreign");

        var error = await Should.ThrowAsync<ApiException>(
            () => PatchAsync(store, story.Id, 1, new WorkItemChanges { AddLinks = [new LinkValue(LinkType.Related, foreign.Id, null, null, null)] }));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Patch_WhenParentIsSetThenCleared_ShouldRecordTheTitleOfTheParent()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "The feature");
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var set = await PatchAsync(store, story.Id, 1, new WorkItemChanges { Parent = new(feature.Id) });
        var cleared = await PatchAsync(store, story.Id, 2, new WorkItemChanges { Parent = new(null) });

        set.Changes.Single().ToLabel.ShouldBe("The feature");
        cleared.Changes.Single().FromLabel.ShouldBe("The feature");
        (await store.GetAsync(story.Id)).ParentType.ShouldBeNull();
    }

    [Test]
    public async Task Patch_WhenFeatureWouldBecomeItsOwnAncestor_ShouldBeValidation()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var outer = await CreateAsync(store, project, WorkItemTypeId.Feature, "Outer");
        var inner = await CreateAsync(store, project, WorkItemTypeId.Feature, "Inner", outer.Id);

        var error = await Should.ThrowAsync<ApiException>(() => PatchAsync(store, outer.Id, 1, new WorkItemChanges { Parent = new(inner.Id) }));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Patch_WhenStateBelongsToAnotherType_ShouldBeValidation()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var error = await Should.ThrowAsync<ApiException>(
            () => PatchAsync(store, story.Id, 1, new WorkItemChanges { State = new(project.StateId(WorkItemTypeId.Bug, StateCategory.InProgress)) }));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Patch_WhenStateChanges_ShouldMoveStateChangedAt()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var created = (await store.GetAsync(story.Id)).StateChangedAt;

        await PatchAsync(store, story.Id, 1, new WorkItemChanges { State = new(project.StateId(WorkItemTypeId.Story, StateCategory.InProgress)) });

        (await store.GetAsync(story.Id)).StateChangedAt.ShouldBeGreaterThan(created);
    }

    [Test]
    public async Task Patch_WhenAssigneeIsUnknown_ShouldBeNotFound()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var error = await Should.ThrowAsync<ApiException>(() => PatchAsync(store, story.Id, 1, new WorkItemChanges { Assignee = new(Guid.NewGuid()) }));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Patch_WhenItemIsUnknown_ShouldBeNotFound()
    {
        var store = PassThroughStore();

        var error = await Should.ThrowAsync<ApiException>(() => PatchAsync(store, Guid.NewGuid(), 1, new WorkItemChanges { Title = new("Nope") }));

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Patch_ShouldCallTheRulesInsideTheTransactionWithBeforeAndTheActor()
    {
        var rules = new TransactionCapturingRules();
        var store = Arrangement.CreateStore(rules, Actor);
        var project = await Arrangement.ArrangeProjectAsync();

        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Ruled");
        await PatchAsync(store, story.Id, 1, new WorkItemChanges { Title = new("Ruled again") });

        rules.Transactions.Count.ShouldBe(2);
        rules.Transactions.ShouldAllBe(t => t != null);
        rules.Before[0].ShouldBeNull();
        rules.Before[1].ShouldNotBeNull().Title.ShouldBe("Ruled");
    }

    [Test]
    public async Task PatchMany_WhenOneFails_ShouldWriteNothing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");

        var error = await Should.ThrowAsync<ApiException>(() => store.PatchManyAsync(
            [
                (first.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Changed") })),
                (second.Id, new WorkItemPatch(1, new WorkItemChanges { Priority = new(9) })),
            ],
            WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        var unchanged = await store.GetAsync(first.Id);
        unchanged.Title.ShouldBe("First");
        unchanged.Revision.ShouldBe(1);
        (await store.GetRevisionsAsync(first.Id)).Count.ShouldBe(1);
    }

    [Test]
    public async Task PatchMany_WhenOneIsStale_ShouldReturnEveryConflictAndWriteNothing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        var third = await CreateAsync(store, project, WorkItemTypeId.Story, "Third");
        await PatchAsync(store, second.Id, 1, new WorkItemChanges { Points = new(1m) });
        await PatchAsync(store, third.Id, 1, new WorkItemChanges { Points = new(1m) });

        var outcome = await store.PatchManyAsync(
            [
                (first.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(3m) })),
                (second.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(3m) })),
                (third.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(3m) })),
            ],
            WriteOptions.User);

        outcome.Written.ShouldBeNull();
        outcome.Conflicts.ShouldNotBeNull().Select(c => c.Current.Id).ShouldBe([second.Id, third.Id]);
        (await store.GetAsync(first.Id)).Points.ShouldBeNull();
    }

    [Test]
    public async Task PatchMany_WhenAllSucceed_ShouldWriteEachItemAndRunTheHookOnce()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        var hooks = 0;
        var options = new WriteOptions((_, _) =>
        {
            hooks++;
            return Task.CompletedTask;
        }, RevisionOrigin.User);

        var outcome = await store.PatchManyAsync(
            [
                (first.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) })),
                (second.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(2m) })),
            ],
            options);

        outcome.Written.ShouldNotBeNull().Select(w => w.Revision).ShouldBe([2, 2]);
        hooks.ShouldBe(1);
    }

    [Test]
    public async Task PatchMany_BeforePatch_ShouldRunOnceAfterTheLocksAndBeforeAnyPatch()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        var calls = 0;
        var seen = (Locks: 0L, Revisions: Array.Empty<int>(), Held: 0L);
        var options = new WriteOptions(null, RevisionOrigin.User, async (transaction, token) =>
        {
            calls++;
            var connection = transaction.Connection!;
            seen = (
                await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    "SELECT count(*) FROM pg_locks WHERE pid = pg_backend_pid() AND locktype = 'advisory' AND granted", transaction: transaction, cancellationToken: token)),
                [.. await connection.QueryAsync<int>(new CommandDefinition("SELECT revision FROM work_item WHERE id = ANY(@ids) ORDER BY id", new { ids = new[] { first.Id, second.Id } }, transaction, cancellationToken: token))],
                await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                    "SELECT count(*) FROM pg_locks l JOIN pg_class c ON c.oid = l.relation WHERE l.pid = pg_backend_pid() AND c.relname = 'work_item' AND l.mode = 'RowShareLock' AND l.granted", transaction: transaction, cancellationToken: token)));
        });

        var outcome = await store.PatchManyAsync(
            [(first.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) })), (second.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(2m) }))], options);

        outcome.Written.ShouldNotBeNull().Count.ShouldBe(2);
        calls.ShouldBe(1);
        seen.Locks.ShouldBeGreaterThan(0);
        seen.Held.ShouldBeGreaterThan(0);
        seen.Revisions.ShouldBe([1, 1]);
    }

    [Test]
    public async Task PatchMany_WhenALaterPatchFails_ShouldRollBackWhatBeforePatchWrote()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var options = new WriteOptions(null, RevisionOrigin.User, async (transaction, token) =>
            await transaction.Connection!.ExecuteAsync(new CommandDefinition("UPDATE project SET description = 'written by the hook' WHERE id = @id", new { id = project.ProjectId }, transaction, cancellationToken: token)));

        await Should.ThrowAsync<ApiException>(() => store.PatchManyAsync(
            [(story.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new(" ") }))], options));

        (await QueryAsync<string>("SELECT description FROM project WHERE id = @id", new { id = project.ProjectId })).ShouldNotBe("written by the hook");
    }

    [Test]
    public async Task PatchMany_WhenStale_ShouldNotRunBeforePatch()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        await PatchAsync(store, story.Id, 1, new WorkItemChanges { Points = new(1m) });
        var calls = 0;
        var options = new WriteOptions(null, RevisionOrigin.User, (_, _) =>
        {
            calls++;
            return Task.CompletedTask;
        });

        var outcome = await store.PatchManyAsync([(story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(2m) }))], options);

        outcome.Conflicts.ShouldNotBeNull().Count.ShouldBe(1);
        calls.ShouldBe(0);
    }

    [Test]
    public async Task Patch_BeforePatch_ShouldRunOnceBeforeThePatch()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");
        var revisions = new List<int>();
        var options = new WriteOptions(null, RevisionOrigin.User, async (transaction, token) =>
            revisions.Add(await transaction.Connection!.ExecuteScalarAsync<int>(new CommandDefinition("SELECT revision FROM work_item WHERE id = @id", new { id = story.Id }, transaction, cancellationToken: token))));

        var outcome = await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) }), options);

        WorkItemContractSupport.Written(outcome).Revision.ShouldBe(2);
        revisions.ShouldBe([1]);
    }

    [Test]
    public async Task PatchMany_WhenAnItemIsGivenTwice_ShouldBeValidation()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var error = await Should.ThrowAsync<ApiException>(() => store.PatchManyAsync(
            [
                (story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) })),
                (story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(2m) })),
            ],
            WriteOptions.User));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Patch_WhenOriginIsJira_ShouldStampTheRevisionWithTheSystem()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var outcome = await store.PatchAsync(story.Id, new WorkItemPatch(1, new WorkItemChanges { Points = new(1m) }), new WriteOptions(null, RevisionOrigin.Jira));

        WorkItemContractSupport.Written(outcome).Revision.ShouldBe(2);
        (await store.GetRevisionsAsync(story.Id))[1].OriginalSystem.ShouldBe("jira");
    }

    private sealed class TransactionCapturingRules : IWorkItemRules
    {
        private readonly List<DbTransaction?> _transactions = [];
        private readonly List<WorkItemSnapshot?> _before = [];

        public IReadOnlyList<DbTransaction?> Transactions => _transactions;

        public IReadOnlyList<WorkItemSnapshot?> Before => _before;

        public Task<RuleOutcome> ApplyAsync(WorkItemSnapshot? before, ProposedChange change, Guid? actorId, DbTransaction? transaction, CancellationToken cancellationToken = default)
        {
            actorId.ShouldBe(Actor);
            _transactions.Add(transaction);
            _before.Add(before);
            return Task.FromResult(new RuleOutcome(change, []));
        }
    }
}
