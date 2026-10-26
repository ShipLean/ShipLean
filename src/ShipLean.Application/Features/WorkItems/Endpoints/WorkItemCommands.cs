using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems.Endpoints;

// What the work item write endpoints do: the access check first, then the store. The store's outcome is returned as it is.
[RegisterPerRequest]
public sealed class WorkItemCommands(IProjectAccess access, IWorkItemStore store)
{
    public async Task<WriteOutcome> CreateAsync(WorkItemCreate create, CancellationToken cancellationToken)
    {
        await access.RequireAsync(create.ProjectId, ProjectRole.Member, cancellationToken);
        return await store.CreateAsync(create, WriteOptions.User, cancellationToken);
    }

    public async Task<WriteOutcome> PatchAsync(Guid id, WorkItemPatch patch, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, id, ProjectRole.Member, cancellationToken);
        return await store.PatchAsync(id, patch, WriteOptions.User, cancellationToken);
    }

    public async Task<WriteOutcome> ReorderAsync(Guid id, int expectedRevision, Guid? beforeItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, id, ProjectRole.Member, cancellationToken);
        return await store.ReorderAsync(id, expectedRevision, beforeItemId, cancellationToken);
    }

    public async Task<WriteOutcome> RemoveAsync(Guid id, int expectedRevision, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, id, ProjectRole.Member, cancellationToken);
        return await store.RemoveAsync(id, expectedRevision, cancellationToken);
    }
}
