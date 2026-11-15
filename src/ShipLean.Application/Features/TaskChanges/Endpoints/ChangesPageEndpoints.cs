using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Endpoints;

// What GET api/work-items/{id}/changes does: the access check first, then the Changes page's read. Commit is "all" or a commit id.
[RegisterPerRequest]
public sealed class ChangesPageEndpoints(IProjectAccess access, ITaskChangesPage page)
{
    public async Task<TaskChangesDto> PageAsync(Guid workItemId, string? commit, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await page.PageAsync(workItemId, string.IsNullOrWhiteSpace(commit) ? "all" : commit, cancellationToken);
    }
}
