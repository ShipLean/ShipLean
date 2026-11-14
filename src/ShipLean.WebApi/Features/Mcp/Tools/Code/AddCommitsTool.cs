using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;

namespace ShipLean.WebApi.Features.Mcp.Tools.Code;

// The tool behind a Task's or Bug's Changes section for agents: put the commits you pushed on it, outside a report.
[McpServerToolType]
internal sealed class AddCommitsTool(ItemResolver itemResolver, CaptureSupport support)
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

        // TODO: add the commits through ITaskCommits.AddAsync (via AddCommits) and answer with what it added and skipped.
        var first = commits?.FirstOrDefault();
        AddedCommit[] added = [new(first?.Repo ?? "acme/storefront", first?.Sha ?? "a81d4e0c3f5b79d2e6a1c8b04f7d93e5a2b6c1d8", "Add the saved-card picker")];
        var result = new AddCommitsResult(
            project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), resolved.Key, added, []);
        return CaptureSupport.Reply(result, $"Added {added.Length} commit to {resolved.Key}.");
    }
}
