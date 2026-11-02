using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using Shouldly;
using static ShipLean.TestSupport.Contracts.WorkItems.WorkItemContractSupport;

namespace ShipLean.TestSupport.Contracts.WorkItems;

// Behaviour of IWorkItemBatchWriter.WriteBatchAsync, whatever the store keeps data in. The store under test also implements
// the writer. Same arrange hooks as WorkItemStoreContract.
[TestFixture]
public abstract class WorkItemBatchWriterContract
{
    protected abstract IWorkItemStore CreateStore(IWorkItemRules rules, Guid actorId);

    protected abstract Task<ArrangedProject> ArrangeProjectAsync();

    // Red against the real store until the batch writer is real.
    [Test]
    public async Task WriteBatch_WhenOneRowStale_ShouldWriteTheOthers()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Story, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Story, "Second");
        var third = await CreateAsync(store, project, WorkItemTypeId.Story, "Third");
        Written(await store.PatchAsync(second.Id, new WorkItemPatch(1, new WorkItemChanges { Title = new("Second, edited") }), WriteOptions.User));

        var outcome = await ((IWorkItemBatchWriter)store).WriteBatchAsync(
            [
                new(first.Id, 1, Points(5), null),
                new(second.Id, 1, Points(3), null),
                new(third.Id, 1, Points(8), null),
            ],
            WriteOptions.User with { Bulk = true });

        outcome.Written.Select(w => w.Id).ShouldBe([first.Id, third.Id]);
        outcome.Stale.Select(s => s.Current.Id).ShouldBe([second.Id]);
        (await store.GetAsync(first.Id)).Points.ShouldBe(5m);
        (await store.GetAsync(third.Id)).Points.ShouldBe(8m);
        (await store.GetAsync(second.Id)).Points.ShouldBeNull();
    }

    // Red against the real store until the batch writer is real.
    [Test]
    public async Task WriteBatch_WhenBlockPlaced_ShouldKeepRequestOrder()
    {
        var store = CreateStore(new PassThroughWorkItemRules(), Actor);
        var project = await ArrangeProjectAsync();
        var x = await CreateAsync(store, project, WorkItemTypeId.Story, "X");
        var y = await CreateAsync(store, project, WorkItemTypeId.Story, "Y");
        var z = await CreateAsync(store, project, WorkItemTypeId.Story, "Z");
        var top = new BatchPlacement(BatchSlot.First, null);

        await ((IWorkItemBatchWriter)store).WriteBatchAsync(
            [new(x.Id, 1, null, top), new(y.Id, 1, null, top)], WriteOptions.User with { Bulk = true });

        // Creates land on top, so the order before the batch was Z, Y, X.
        var order = new List<(Guid Id, string Order)>();
        foreach (var id in new[] { x.Id, y.Id, z.Id })
        {
            order.Add((id, (await store.GetAsync(id)).BacklogOrder));
        }

        order.OrderBy(o => o.Order, StringComparer.Ordinal).Select(o => o.Id).ShouldBe([x.Id, y.Id, z.Id]);
    }

    private static WorkItemChanges Points(decimal points) => new() { Points = new(points) };
}
