using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Backlog.Stories;

// What the backlog write endpoints do: the access check first, then the checks every action shares, then the write.
[RegisterPerRequest]
public sealed partial class BacklogWriteCommands(IProjectAccess access, IWorkItemStore store, IWorkItemBatchWriter batch, IBacklogChecks checks)
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

        var ids = items.Select(i => i.Id).ToHashSet();
        foreach (var item in items)
        {
            if (item.Placement is { Slot: BatchSlot.Before, BeforeItemId: { } before } && ids.Contains(before))
            {
                throw ApiException.Validation("A row can't be placed before a row of the same write.");
            }

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
