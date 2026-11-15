using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Endpoints;

// What GET api/work-items/{id}/commits does: the access check first, then the Changes section's read.
[RegisterPerRequest]
public sealed class CommitsSectionEndpoints(IProjectAccess access, ITaskCommitsSection section)
{
    public async Task<TaskCommitsSectionDto> SectionAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        return await section.SectionAsync(workItemId, cancellationToken);
    }
}
