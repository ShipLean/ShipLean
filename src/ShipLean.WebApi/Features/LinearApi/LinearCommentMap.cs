using ShipLean.Application.Features.LinearApi;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.LinearApi;

internal static class LinearCommentMap
{
    // issueUrl is the link to the item the comment is on; ShipLean has no address for a single comment.
    public static IReadOnlyDictionary<string, object?> Of(LinearComment comment, string issueUrl) => new Dictionary<string, object?>
    {
        ["id"] = comment.Id.ToString(),
        ["body"] = comment.Body,
        ["createdAt"] = comment.CreatedAt,
        ["updatedAt"] = comment.UpdatedAt,
        ["issueId"] = comment.IssueId.ToString(),
        ["issue"] = new Dictionary<string, object?> { ["id"] = comment.IssueId.ToString() },
        ["url"] = issueUrl,
        ["user"] = comment.Author is { } author ? LinearPersonMap.Of(author) : null,
    };
}
