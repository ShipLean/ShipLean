using Agoda.IoC.Core;
using ShipLean.Application.Features.TaskChanges.Comments;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Endpoints;

// What the code comment routes do: the access check on the work item first (a comment id is checked against that item), then the write.
[RegisterPerRequest]
public sealed class CommentEndpoints(IProjectAccess access, ICodeComments comments, TimeProvider time)
{
    public async Task<CodeCommentDto> AddAsync(Guid workItemId, AddCodeCommentRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        var side = request.Side switch
        {
            "old" => CodeSide.Old,
            "new" => CodeSide.New,
            _ => throw ApiException.Validation("Side is old or new.", "side"),
        };
        var added = await comments.AddAsync(
            workItemId, new NewCodeComment(request.Repo, request.Sha, request.Path, side, request.Line, request.StartLine, request.LineText ?? string.Empty, request.Body), cancellationToken);
        return CodeCommentDtos.From(added, time);
    }

    public async Task<CodeCommentDto> ReplyAsync(Guid workItemId, Guid commentId, ReplyCodeCommentRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        return CodeCommentDtos.From(await comments.ReplyAsync(workItemId, commentId, request.Body, cancellationToken), time);
    }

    public async Task ResolveAsync(Guid workItemId, Guid commentId, ResolveCodeCommentRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);

        await comments.ResolveAsync(workItemId, commentId, request.Resolved, cancellationToken);
    }
}
