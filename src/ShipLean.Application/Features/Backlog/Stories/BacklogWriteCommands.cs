using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Backlog.Stories;

// What the backlog write endpoints do: the access check first, then the checks every action shares, then the write.
[RegisterPerRequest]
public sealed partial class BacklogWriteCommands(IProjectAccess access, IWorkItemStore store, IWorkItemBatchWriter batch, IWorkItemBlockWriter blocks, IBacklogChecks checks)
{
    public async Task<BulkWriteResult> BulkAsync(Guid projectId, BulkWriteRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        var items = request.Items;
        RequireWithinLimit(items.Count, "rows");
        if (items.Select(i => i.Id).Distinct().Count() != items.Count)
        {
            throw ApiException.Validation("A row can be in a bulk write only once.");
        }

        foreach (var item in items)
        {
            CheckItem(item);
        }

        RequireNoBeforeWithinBlock(items);
        foreach (var item in items)
        {
            var row = await store.GetAsync(item.Id, cancellationToken);
            if (row.ProjectId != projectId)
            {
                throw ApiException.NotFound("Work item not found.");
            }
        }

        var options = items.Count > 1 ? WriteOptions.User with { Bulk = true } : WriteOptions.User;
        var writes = items.Select(i => new BatchWrite(i.Id, i.ExpectedRevision, i.Changes, i.Placement is { } p ? new BatchPlacement(p.Slot, p.BeforeItemId) : null)).ToList();
        var outcome = await batch.WriteBatchAsync(writes, options, cancellationToken);
        return new BulkWriteResult(outcome.Written, outcome.Stale);
    }

    // Consecutive items with an equal placement form a block. A "before" that names a row of its own block can't land.
    private static void RequireNoBeforeWithinBlock(IReadOnlyList<BulkWriteItem> items)
    {
        for (var start = 0; start < items.Count;)
        {
            var end = start + 1;
            while (end < items.Count && Equals(items[end].Placement, items[start].Placement))
            {
                end++;
            }

            if (items[start].Placement is { Slot: BatchSlot.Before, BeforeItemId: { } before } && items.Skip(start).Take(end - start).Any(i => i.Id == before))
            {
                throw ApiException.Validation("A row can't be placed before a row of the same block.");
            }

            start = end;
        }
    }

    private static void CheckItem(BulkWriteItem item)
    {
        if (item.Changes is null && item.Placement is null)
        {
            throw ApiException.Validation("A row needs changes, a placement or both.");
        }

        if (item.Placement is { } placement && (placement.Slot == BatchSlot.Before) != (placement.BeforeItemId is not null))
        {
            throw ApiException.Validation("A placement names the row to go before if and only if its slot is before.");
        }
    }

    private static void RequireWithinLimit(int count, string what)
    {
        if (count is < 1 or > BulkLimits.MaxRows)
        {
            throw ApiException.Validation($"Send 1 to {BulkLimits.MaxRows} {what}.");
        }
    }
}
