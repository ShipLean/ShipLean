using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Endpoints;

// What POST api/work-items/{id}/commits does: the access check first, then the add.
[RegisterPerRequest]
public sealed class CommitEndpoints(IProjectAccess access, ITaskCommits commits, IRepoAddresses addresses, ITaskCommitsSection section)
{
    public async Task<TaskCommitsSectionDto> AddAsync(Guid workItemId, AddTaskCommitRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        CommitRef commit;
        if (string.IsNullOrWhiteSpace(request.Repo))
        {
            var parts = await addresses.ParseCommitUrlAsync(request.Sha, cancellationToken);
            commit = new CommitRef(parts.Repo.WebUrl, parts.Sha);
        }
        else
        {
            commit = new CommitRef(request.Repo, request.Sha);
        }

        await commits.AddAsync(workItemId, [commit], CommitSource.Form, cancellationToken);
        return await section.SectionAsync(workItemId, cancellationToken);
    }
}
