using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Endpoints;

// What the code comment routes do: the access check on the work item first (a comment id is checked against that item), then the write.
[RegisterPerRequest]
public sealed class CommentEndpoints(IProjectAccess access)
{
    public async Task<CodeCommentDto> AddAsync(Guid workItemId, AddCodeCommentRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        // TODO: add the comment through ICodeComments.AddAsync.
        return new CodeCommentDto(
            Guid.Parse("0000000d-0000-4000-8001-000000000001"), null, request.Repo, request.Sha, request.Path, request.Side, request.Line,
            request.StartLine, request.LineText, $"<p>{System.Net.WebUtility.HtmlEncode(request.Body)}</p>", "Joel", false, "Today", false, false, false, []);
    }

    public async Task<CodeCommentDto> ReplyAsync(Guid workItemId, Guid commentId, ReplyCodeCommentRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        // TODO: reply through ICodeComments.ReplyAsync.
        return new CodeCommentDto(
            Guid.Parse("0000000d-0000-4000-8001-000000000002"), commentId, null, null, null, null, null, null, null,
            $"<p>{System.Net.WebUtility.HtmlEncode(request.Body)}</p>", "Joel", false, "Today", false, false, false, []);
    }

    public async Task ResolveAsync(Guid workItemId, Guid commentId, ResolveCodeCommentRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        // TODO: resolve through ICodeComments.ResolveAsync(workItemId, commentId, request.Resolved).
    }
}
