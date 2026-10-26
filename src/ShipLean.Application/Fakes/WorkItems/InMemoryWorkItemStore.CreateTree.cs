using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Fakes.WorkItems;

public sealed partial class InMemoryWorkItemStore
{
    public async Task<WorkItemTreeOutcome> CreateTreeAsync(WorkItemTreeCreate tree, WriteOptions options, CancellationToken cancellationToken = default)
    {
        if (tree.Items.Count == 0)
        {
            return new WorkItemTreeOutcome([]);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var world = _world.Clone();
            // As CreateMany: the tree lands at the top in key order, the first item first and the rest in front of what was on top before.
            var top = world.Items.Values.Where(i => i.ProjectId == tree.ProjectId).OrderBy(i => i.BacklogOrder, StringComparer.Ordinal).FirstOrDefault()?.Id;
            var first = true;

            async Task<IReadOnlyList<WorkItemTreeOutcome.Node>> CreateAllAsync(IReadOnlyList<WorkItemTreeCreate.Node> nodes, Guid? parentId)
            {
                var created = new List<WorkItemTreeOutcome.Node>(nodes.Count);
                foreach (var node in nodes)
                {
                    var written = (await CreateInAsync(world, node.CreateUnder(tree.ProjectId, parentId), options, first, top, cancellationToken)).Written!;
                    first = false;
                    created.Add(new(written.Id, written.Key, await CreateAllAsync(node.Children, written.Id)));
                }

                return created;
            }

            var outcome = new WorkItemTreeOutcome(await CreateAllAsync(tree.Items, tree.ParentId));
            await RunHookAsync(options, cancellationToken);
            _world = world;
            return outcome;
        }
        finally
        {
            _gate.Release();
        }
    }
}
