using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Endpoints;

// What GET api/work-items/{id}/changes does: the access check first, then the Changes page's read. Commit is "all" or a commit id.
[RegisterPerRequest]
public sealed class ChangesPageEndpoints(IProjectAccess access)
{
    public async Task<TaskChangesDto> PageAsync(Guid workItemId, string? commit, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        // TODO: return ITaskChangesPage.PageAsync(workItemId, commit ?? "all") instead of this made-up page.
        var row = new TaskCommitRowDto(
            Guid.Parse("0000000c-0000-4000-8001-000000000001"), "acme/storefront", new UriBuilder(Uri.UriSchemeHttps, "github.com", -1, "acme/storefront").Uri.ToString(), "github",
            "a81d4e0c3f5b79d2e6a1c8b04f7d93e5a2b6c1d8", "a81d4e0", "Add the saved-card picker", "Priya Nair", "Today", "Claude Code", true,
            1, 0, 1, new UriBuilder(Uri.UriSchemeHttps, "github.com", -1, "acme/storefront/commit/a81d4e0c3f5b79d2e6a1c8b04f7d93e5a2b6c1d8").Uri.ToString(), "ok");
        const string Patch = "@@ -0,0 +1 @@\n+export const savedCards = [];\n";
        DiffFileDto file = new("src/checkout/SavedCards.ts", null, "added", 1, 0, Patch, "0000000000000000000000000000000000000000", 0);
        return new TaskChangesDto(
            workItemId, "SHIP-1", "Made-up task", "task", "inProgress", [row], string.IsNullOrWhiteSpace(commit) ? "all" : commit, "single",
            row.Id.ToString(), [new DiffPartDto(row.Repo, row.Sha, null, row.Sha, [file])], [], 0);
    }
}
