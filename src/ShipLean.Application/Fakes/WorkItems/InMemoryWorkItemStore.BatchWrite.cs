using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Fakes.WorkItems;

public sealed partial class InMemoryWorkItemStore : IWorkItemBatchWriter
{
    public async Task<BatchWriteOutcome> WriteBatchAsync(IReadOnlyList<BatchWrite> items, WriteOptions options, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var world = _world.Clone();
            var stamp = Stamp(options);
            var written = new List<WriteResult>();
            var stale = new List<ConflictDto>();
            var placed = new List<(Guid Id, BatchPlacement Placement)>();
            foreach (var item in items)
            {
                var before = Load(world, item.Id);
                if (item.Changes is not null && before.Revision != item.ExpectedRevision)
                {
                    stale.Add(BuilderFor(world).Conflict(before, item.ExpectedRevision));
                    continue;
                }

                written.Add(item.Changes is null
                    ? new WriteResult(before.Id, before.Key, before.Revision, [], [])
                    : await PatchOneAsync(world, before, item.Changes, null, stamp, cancellationToken));
                if (item.Placement is { } placement)
                {
                    placed.Add((item.Id, placement));
                }
            }

            PlaceBlocks(world, placed);
            await RunHookAsync(options, cancellationToken);
            _world = world;
            return new BatchWriteOutcome(written, stale);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Consecutive rows with an equal placement form a block that lands in request order.
    private static void PlaceBlocks(InMemoryWorld world, List<(Guid Id, BatchPlacement Placement)> placed)
    {
        for (var start = 0; start < placed.Count;)
        {
            var end = start;
            while (end < placed.Count && placed[end].Placement == placed[start].Placement)
            {
                end++;
            }

            var block = placed.Skip(start).Take(end - start).Select(p => p.Id).ToList();
            var placement = placed[start].Placement;
            foreach (var id in block)
            {
                var item = world.Items[id];
                var anchor = AnchorFor(world, item.ProjectId, placement, block);
                world.Items[id] = item with { BacklogOrder = InMemoryOrder.BacklogKey(world, item.ProjectId, id, anchor, false) };
            }

            start = end;
        }
    }

    // The row a block member goes in front of: the top of the others for first, a named row that is still there for before, none (last) otherwise.
    private static Guid? AnchorFor(InMemoryWorld world, Guid projectId, BatchPlacement placement, List<Guid> block)
    {
        var others = world.Items.Values.Where(i => i.ProjectId == projectId && !block.Contains(i.Id));
        switch (placement.Slot)
        {
            case BatchSlot.First:
                return others.OrderBy(i => i.BacklogOrder, StringComparer.Ordinal).FirstOrDefault()?.Id;
            case BatchSlot.Before:
                if (placement.BeforeItemId is { } before && block.Contains(before))
                {
                    throw ApiException.Validation("A row can't be placed before a row of the same write.");
                }

                return others.Any(i => i.Id == placement.BeforeItemId) ? placement.BeforeItemId : null;
            default:
                return null;
        }
    }
}
