using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Endpoints;

// What POST api/work-items/{id}/commits does: the access check first, then the add.
[RegisterPerRequest]
public sealed class CommitEndpoints(IProjectAccess access)
{
    public async Task<TaskCommitsSectionDto> AddAsync(Guid workItemId, AddTaskCommitRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        // TODO: add the commit through ITaskCommits.AddAsync (a commit URL when Repo is empty) and return the item's section.
        var commit = new TaskCommitRowDto(
            Guid.Parse("0000000c-0000-4000-8001-000000000001"), request.Repo ?? "acme/storefront", new UriBuilder(Uri.UriSchemeHttps, "github.com", -1, "acme/storefront").Uri.ToString(), "github",
            "a81d4e0c3f5b79d2e6a1c8b04f7d93e5a2b6c1d8", "a81d4e0", "Add the saved-card picker", "Priya Nair", "Today", "Joel", false,
            42, 7, 2, new UriBuilder(Uri.UriSchemeHttps, "github.com", -1, "acme/storefront/commit/a81d4e0c3f5b79d2e6a1c8b04f7d93e5a2b6c1d8").Uri.ToString(), "ok");
        return new TaskCommitsSectionDto(
            workItemId, [commit], new TaskCommitTotalsDto(1, 2, 42, 7),
            [new TaskChangeFilePathDto("acme/storefront", "src/checkout/SavedCards.tsx"), new TaskChangeFilePathDto("acme/storefront", "src/checkout/index.ts")],
            0, commit.Id.ToString());
    }
}
