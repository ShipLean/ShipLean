using ShipLean.Contracts.Refs;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

// The commits and open code comments get_work_item and get_brief show. Only a Task or Bug has them; any other type gets two empty lists.
internal static class ItemCodeReads
{
    public static bool Holds(WorkItemTypeId type) => type is WorkItemTypeId.Task or WorkItemTypeId.Bug;

    public static async Task<(IReadOnlyList<ItemCommit> Commits, IReadOnlyList<OpenCodeComment> Open)> ReadAsync(
        ITaskCommits commits, ICodeComments codeComments, WorkItemTypeId type, Guid workItemId, CancellationToken cancellationToken)
    {
        if (!Holds(type))
        {
            return ([], []);
        }
        var listed = await commits.ListAsync(workItemId, cancellationToken);
        var open = await codeComments.OpenAsync(workItemId, cancellationToken);
        return (
            [.. listed.Select(c => new ItemCommit(c.Repo.Name, c.Sha, c.Subject, c.Additions, c.Deletions))],
            [.. open.Select(c => new OpenCodeComment(
                c.Id, c.Repo ?? string.Empty, c.Path ?? string.Empty, c.Side == CodeSide.Old ? "old" : "new", c.Line ?? 0, c.Sha ?? string.Empty, c.LineText ?? string.Empty, c.Body, c.By))]);
    }
}
