using ShipLean.Core;
using ShipLean.Contracts.TaskChanges;
using ShipLean.Contracts.TaskChanges.Dtos;

namespace ShipLean.Application.Features.TaskChanges.Comments;

// A CodeComment as the wire shape: side as "old" or "new", the time as a label.
public static class CodeCommentDtos
{
    public static CodeCommentDto From(CodeComment comment, TimeProvider time) => new(
        comment.Id,
        comment.ParentId,
        comment.Repo,
        comment.Sha,
        comment.Path,
        SideName(comment.Side),
        comment.Line,
        comment.StartLine,
        comment.LineText,
        comment.BodyHtml,
        comment.By,
        comment.ByAgent,
        DisplayDate.Relative(comment.CreatedAt, time),
        comment.ResolvedAt is not null,
        comment.OutdatedAt is not null,
        comment.SentBackAt is not null,
        [.. comment.Replies.Select(r => From(r, time))]);

    private static string? SideName(CodeSide? side)
    {
        if (side is null)
        {
            return null;
        }

        return side == CodeSide.Old ? "old" : "new";
    }
}
