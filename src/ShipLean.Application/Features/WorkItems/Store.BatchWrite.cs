using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

public sealed partial class WorkItemStore : IWorkItemBatchWriter
{
    // The partial write behind the bulk endpoint, in one transaction: the project's write lock, then every row lock, once (the order
    // PatchMany takes). A row whose expected revision is out of date is left out and reported; the others are patched in request
    // order, then each block of equal placements is placed in request order. A bad value on any row throws and rolls everything back.
    // A position-only row (no changes) is not revision-checked, as a reorder isn't.
    public Task<BatchWriteOutcome> WriteBatchAsync(IReadOnlyList<BatchWrite> items, WriteOptions options, CancellationToken cancellationToken = default)
        => WriteAsync(options, async scope =>
        {
            if (items.Select(i => i.Id).Distinct().Count() != items.Count)
            {
                throw ApiException.Validation("A work item can only appear once in a batch.");
            }

            var rows = await LockAsync(scope, [.. items.Select(i => i.Id)], cancellationToken);
            var stale = new List<ConflictDto>();
            var live = new List<BatchWrite>();
            foreach (var item in items)
            {
                if (item.Changes is not null && rows[item.Id].Revision != item.ExpectedRevision)
                {
                    stale.Add(await _reads.ConflictAsync(scope.Connection, scope.Transaction, item.Id, item.ExpectedRevision, cancellationToken));
                }
                else
                {
                    live.Add(item);
                }
            }

            if (live.Count == 0)
            {
                return (new BatchWriteOutcome([], stale), false);
            }

            await RunBeforePatchAsync(scope, options, cancellationToken);
            var written = new List<WriteResult>(live.Count);
            foreach (var item in live)
            {
                written.Add(item.Changes is null
                    ? new WriteResult(item.Id, rows[item.Id].Key, rows[item.Id].Revision, [], [])
                    : await PatchOneAsync(scope, rows[item.Id], item.Changes, cancellationToken));
            }

            await PlaceBlocksAsync(scope, live.Where(i => i.Placement is not null).ToList(), rows, cancellationToken);
            return (new BatchWriteOutcome(written, stale), true);
        }, cancellationToken);

    // Consecutive rows with an equal placement form a block, which lands in request order whichever slot it is for.
    private static async Task PlaceBlocksAsync(WriteScope scope, IReadOnlyList<BatchWrite> placed, IReadOnlyDictionary<Guid, WorkItemRow> rows, CancellationToken cancellationToken)
    {
        for (var start = 0; start < placed.Count;)
        {
            var end = start + 1;
            while (end < placed.Count && placed[end].Placement == placed[start].Placement)
            {
                end++;
            }

            var block = placed.Skip(start).Take(end - start).Select(i => i.Id).ToList();
            var placement = placed[start].Placement!;
            if (placement.Slot == BatchSlot.Before && placement.BeforeItemId is { } target && block.Contains(target))
            {
                throw ApiException.Validation("A row can't be placed before a row of the same write.");
            }

            var projectId = rows[block[0]].ProjectId;
            var orderScope = OrderScope.Backlog(projectId);
            var anchor = placement.Slot switch
            {
                BatchSlot.First => await TopOutsideAsync(scope, projectId, block, cancellationToken),
                BatchSlot.Before => placement.BeforeItemId,
                _ => null,
            };
            foreach (var id in block)
            {
                // Every member goes in front of the same anchor, so the block stays in request order; no anchor means the bottom.
                var order = await WorkItemOrdering.KeyAsync(scope, orderScope, id, anchor is null ? Slot.Last : Slot.BeforeOrLast, anchor, cancellationToken);
                await scope.Connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE work_item SET backlog_order = @order WHERE id = @id", new { id, order }, scope.Transaction, cancellationToken: cancellationToken));
                scope.Publish([new WorkItemReordered(projectId, id, order)]);
            }

            start = end;
        }
    }

    // The top row of the backlog that isn't part of the block, or null when the block is the whole backlog.
    private static Task<Guid?> TopOutsideAsync(WriteScope scope, Guid projectId, List<Guid> block, CancellationToken cancellationToken)
        => scope.Connection.QueryFirstOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM work_item WHERE project_id = @projectId AND id <> ALL(@block) ORDER BY backlog_order, id LIMIT 1",
            new { projectId, block = block.ToArray() }, scope.Transaction, cancellationToken: cancellationToken));
}
