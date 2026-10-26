using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// Two writes to one project at once, each held inside its transaction by the barrier until the other has joined it.
[TestFixture]
public class WorkItemStoreConcurrencyTests : PostgresWorkItemStoreTestBase
{
    private static readonly TimeSpan _barrierTimeout = TimeSpan.FromSeconds(2);

    [Test]
    public async Task Link_WhenTwoItemsLinkEachOtherAtOnce_ShouldNotDeadlock()
    {
        var setup = PassThroughStore();
        var store = Arrangement.CreateStore(new BarrierWorkItemRules(_barrierTimeout), Actor);
        var project = await Arrangement.ArrangeProjectAsync();
        var a = await CreateAsync(setup, project, WorkItemTypeId.Story, "A");
        var b = await CreateAsync(setup, project, WorkItemTypeId.Story, "B");

        var results = await Task.WhenAll(
            LinkAsync(store, a.Id, b.Id),
            LinkAsync(store, b.Id, a.Id));

        results.Select(r => r.Revision).ShouldBe([2, 2]);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item_link WHERE source_id IN (@a, @b) AND archived_at IS NULL", new { a = a.Id, b = b.Id })).ShouldBe(2);
    }

    [Test]
    public async Task Reparent_WhenTwoFeaturesCrossAtOnce_ShouldRejectTheCycle()
    {
        var setup = PassThroughStore();
        var store = Arrangement.CreateStore(new BarrierWorkItemRules(_barrierTimeout), Actor);
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(setup, project, WorkItemTypeId.Feature, "F1");
        var second = await CreateAsync(setup, project, WorkItemTypeId.Feature, "F2");

        var outcomes = await Task.WhenAll(
            TryAsync(() => PatchAsync(store, first.Id, 1, new WorkItemChanges { Parent = new(second.Id) })),
            TryAsync(() => PatchAsync(store, second.Id, 1, new WorkItemChanges { Parent = new(first.Id) })));

        outcomes.Count(e => e is null).ShouldBe(1);
        outcomes.Single(e => e is not null)!.Code.ShouldBe(ApiErrorCodes.Validation);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE parent_id IS NOT NULL AND project_id = @id", new { id = project.ProjectId })).ShouldBe(1);
    }

    [Test]
    public async Task Move_WhenTwoDropsAtOnce_ShouldGiveDistinctKeys()
    {
        var setup = PassThroughStore();
        var store = Arrangement.CreateStore(new BarrierWorkItemRules(_barrierTimeout), Actor);
        var project = await Arrangement.ArrangeProjectAsync();
        var a = await CreateAsync(setup, project, WorkItemTypeId.Story, "A");
        var b = await CreateAsync(setup, project, WorkItemTypeId.Story, "B");
        var column = project.ColumnId(1);

        await Task.WhenAll(
            store.MoveAsync(a.Id, new MoveTarget(1, column, false, null, null, null), WriteOptions.User),
            store.MoveAsync(b.Id, new MoveTarget(1, column, false, null, null, null), WriteOptions.User));

        var orderOfA = (await setup.GetAsync(a.Id)).ColumnOrder.ShouldNotBeNull();
        var orderOfB = (await setup.GetAsync(b.Id)).ColumnOrder.ShouldNotBeNull();
        orderOfA.ShouldNotBe(orderOfB);
        var secondInColumn = string.CompareOrdinal(orderOfA, orderOfB) > 0 ? a.Id : b.Id;
        var third = await CreateAsync(setup, project, WorkItemTypeId.Story, "C");
        Written(await setup.MoveAsync(third.Id, new MoveTarget(1, column, false, null, secondInColumn, null), WriteOptions.User)).Revision.ShouldBe(1);
    }

    [Test]
    public async Task Create_WhenConcurrent_ShouldGiveDistinctBacklogKeys()
    {
        var store = Arrangement.CreateStore(new BarrierWorkItemRules(_barrierTimeout), Actor);
        var project = await Arrangement.ArrangeProjectAsync();

        await Task.WhenAll(Enumerable.Range(0, 6).Select(i => CreateAsync(store, project, WorkItemTypeId.Story, $"Parallel {i}")));

        (await QueryAsync<int>("SELECT count(DISTINCT backlog_order)::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBe(6);
        (await QueryAsync<int>("SELECT count(DISTINCT column_order)::int FROM work_item WHERE project_id = @id", new { id = project.ProjectId })).ShouldBe(6);
    }

    private static Task<WriteResult> LinkAsync(IWorkItemStore store, Guid from, Guid to)
        => PatchAsync(store, from, 1, new WorkItemChanges { AddLinks = [new LinkValue(LinkType.Related, to, null, null, null)] });

    private static async Task<ApiException?> TryAsync(Func<Task<WriteResult>> write)
    {
        try
        {
            await write();
            return null;
        }
        catch (ApiException error)
        {
            return error;
        }
    }

    [Test]
    public async Task PatchMany_WhenItemsSpanProjects_ShouldTakeTheProjectLocksInLockKeyOrder()
    {
        var store = PassThroughStore();
        var projects = new List<ArrangedProject>();
        for (var i = 0; i < 8; i++)
        {
            projects.Add(await Arrangement.ArrangeProjectAsync());
        }

        // Two projects whose ids sort one way and whose lock keys the other.
        var ids = projects.Select(p => p.ProjectId).ToArray();
        var byId = await QueryAsync<Guid[]>("SELECT array_agg(p ORDER BY p) FROM unnest(@ids) AS p", new { ids });
        var keyOf = new Dictionary<Guid, int>();
        foreach (var id in ids)
        {
            keyOf[id] = await QueryAsync<int>("SELECT hashtext(@id::text)", new { id });
        }

        var (low, high) = Enumerable.Range(0, byId.Length)
            .SelectMany(i => Enumerable.Range(i + 1, byId.Length - i - 1).Select(j => (First: byId[i], Second: byId[j])))
            .Where(pair => keyOf[pair.First] > keyOf[pair.Second])
            .Select(pair => (Low: pair.Second, High: pair.First))
            .First();
        var lowItem = await CreateAsync(store, projects.Single(p => p.ProjectId == low), WorkItemTypeId.Story, "Low");
        var highItem = await CreateAsync(store, projects.Single(p => p.ProjectId == high), WorkItemTypeId.Story, "High");
        const int LOCK_NAMESPACE = 0x57_49_53_4C;
        await using var holder = await Arrangement.Database.OpenAsync();
        await using var holding = await holder.BeginTransactionAsync();
        await holder.ExecuteAsync("SELECT pg_advisory_xact_lock(@ns, @key)", new { ns = LOCK_NAMESPACE, key = keyOf[high] }, holding);

        var batch = store.PatchManyAsync(
            [(highItem.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("High again") })), (lowItem.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Low again") }))],
            WriteOptions.User);
        var waiter = 0;
        while (waiter == 0)
        {
            waiter = await QueryAsync<int>(
                "SELECT coalesce(max(pid), 0)::int FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND classid::bigint = @ns AND objid::bigint = (@key::bigint & 4294967295)",
                new { ns = LOCK_NAMESPACE, key = keyOf[high] });
        }

        var heldByTheBatch = await QueryAsync<int>(
            "SELECT count(*)::int FROM pg_locks WHERE pid = @waiter AND granted AND locktype = 'advisory' AND classid::bigint = @ns AND objid::bigint = (@key::bigint & 4294967295)",
            new { waiter, ns = LOCK_NAMESPACE, key = keyOf[low] });
        await holding.CommitAsync();
        var outcome = await batch;

        heldByTheBatch.ShouldBe(1);
        outcome.Written.ShouldNotBeNull();
    }
}
