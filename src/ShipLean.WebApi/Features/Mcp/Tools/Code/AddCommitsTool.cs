using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.TaskChanges;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;

namespace ShipLean.WebApi.Features.Mcp.Tools.Code;

// The tool behind a Task's or Bug's Changes section for agents: put the commits you pushed on it, outside a report.
[McpServerToolType]
internal sealed class AddCommitsTool(ItemResolver itemResolver, CaptureSupport support, ITaskCommits taskCommits, IRepoAddresses repoAddresses)
{
    [McpServerTool(Name = "add_commits"), McpScope(McpScopes.Write)]
    [Description("Put commits on a Task or Bug so people can review their diff in ShipLean's Changes page. Send the repository (as git remote get-url origin prints it) and the full sha of each commit you pushed, at most 25 a call. The call is all or nothing: if any commit can't be read, nothing is added and the error names it. A commit the item already has is skipped. Only Tasks and Bugs take commits. report_task also takes commits, with the same shape.")]
    public async Task<CallToolResult> AddCommits(
        [Description("The Task's or Bug's key, such as SHIP-12.")] string item,
        [Description("The commits, 1 to 25, in the order you made them.")] IReadOnlyList<CommitInput> commits,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var project = await support.ProjectOfAsync(resolved, cancellationToken);

        var refs = new List<CommitRef>();
        for (var i = 0; i < (commits?.Count ?? 0); i++)
        {
            refs.Add(await RefOfAsync(commits![i], i, cancellationToken));
        }

        var outcome = await taskCommits.AddAsync(resolved.Id, refs, CommitSource.AddCommits, cancellationToken);
        AddedCommit[] added = [.. outcome.Added.Select(Of)];
        AddedCommit[] skipped = [.. outcome.Skipped.Select(Of)];
        var result = new AddCommitsResult(
            project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), resolved.Key, added, skipped);
        return CaptureSupport.Reply(result, $"Added {added.Length} commits to {resolved.Key} ({skipped.Length} already there).");
    }

    // A blank repo means sha is the commit's URL.
    private async Task<CommitRef> RefOfAsync(CommitInput input, int index, CancellationToken cancellationToken)
    {
        var sha = input?.Sha;
        if (string.IsNullOrWhiteSpace(sha))
        {
            throw ApiException.Validation("A commit needs a sha, or its URL when repo is left out.", $"commits[{index}].sha");
        }
        if (string.IsNullOrWhiteSpace(input!.Repo))
        {
            var parts = await repoAddresses.ParseCommitUrlAsync(sha, cancellationToken);
            return new CommitRef(parts.Repo.WebUrl, parts.Sha);
        }
        return new CommitRef(input.Repo, sha);
    }

    private static AddedCommit Of(TaskCommit commit) => new(commit.Repo.Name, commit.Sha, commit.Subject);
}
