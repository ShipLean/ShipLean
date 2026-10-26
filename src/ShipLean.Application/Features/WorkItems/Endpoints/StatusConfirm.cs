using Agoda.IoC.Core;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Contracts;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems.Endpoints;

// "Still true": the access check first, then ConfirmStatus alone, which stamps Verified at in one revision. The item is read
// back after a write so the browser gets the new stamp and label; a stale revision comes back as the store's conflict.
[RegisterPerRequest]
public sealed class StatusConfirm(IProjectAccess access, IWorkItemStore store, WorkItemReads reads)
{
    public async Task<(WorkItemDto? Item, ConflictDto? Conflict)> ConfirmAsync(Guid id, int expectedRevision, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, id, ProjectRole.Member, cancellationToken);
        var patch = new WorkItemPatch(expectedRevision, new WorkItemChanges { ConfirmStatus = true });
        var outcome = await store.PatchAsync(id, patch, WriteOptions.User, cancellationToken);
        if (outcome.Conflict is { } conflict)
        {
            return (null, conflict);
        }

        return (await reads.GetAsync(id, cancellationToken), null);
    }
}
