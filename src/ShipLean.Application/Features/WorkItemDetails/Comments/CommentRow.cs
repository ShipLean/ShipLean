using ShipLean.Application.Identity;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItemDetails.Comments;

// A comment joined to its author, as Select reads it. Dapper maps the columns onto the constructor by name. The author
// columns are all null for an imported comment whose author has no user. A comment is "edited" when updated_at is after
// created_at (an import writes both the same).
internal sealed record CommentRow(
    Guid Id,
    Guid WorkItemId,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? AgentName,
    Guid? AuthorId,
    string? AuthorName,
    string? AuthorInitials,
    string? AuthorColour,
    string? AuthorPhotoUrl,
    DateTimeOffset? SupersededAt = null,
    Guid? SupersededById = null,
    string? SupersededByName = null,
    string? SupersededByInitials = null,
    string? SupersededByColour = null,
    string? SupersededByPhotoUrl = null)
{
    public const string Columns =
        $"""
        c.id, c.work_item_id AS workItemId, c.body, c.created_at AS createdAt, c.updated_at AS updatedAt, c.agent_name AS agentName,
        u.id AS authorId, u.name AS authorName, u.initials AS authorInitials, u.avatar_colour AS authorColour, {UserRefs.PhotoUrlSql} AS authorPhotoUrl,
        c.superseded_at AS supersededAt, s.id AS supersededById, s.name AS supersededByName, s.initials AS supersededByInitials,
        s.avatar_colour AS supersededByColour,
        CASE WHEN s.avatar_photo_blob IS NOT NULL THEN '/api/users/' || s.id::text || '/photo?v=' || s.photo_version END AS supersededByPhotoUrl
        """;

    public const string From = "FROM comment c LEFT JOIN app_user u ON u.id = c.author_id LEFT JOIN app_user s ON s.id = c.superseded_by";

    public CommentDto ToDto(Markdown.MarkdownRenderer renderer, TimeProvider clock)
    {
        var rendered = renderer.Render(Body);
        var edited = UpdatedAt > CreatedAt ? UpdatedAt : (DateTimeOffset?)null;
        UserRef? author = AuthorId is { } id ? new UserRef(id, AuthorName!, AuthorInitials!, AuthorColour!, AuthorPhotoUrl) : null;
        var superseded = SupersededAt is { } markedAt && SupersededById is { } by
            ? new CommentSuperseded(markedAt, DisplayDate.Relative(markedAt, clock), new UserRef(by, SupersededByName!, SupersededByInitials!, SupersededByColour!, SupersededByPhotoUrl))
            : null;
        return new CommentDto(
            Id, WorkItemId, author, rendered.Markdown, rendered.Html,
            CreatedAt, DisplayDate.Relative(CreatedAt, clock),
            edited, edited is { } at ? DisplayDate.Relative(at, clock) : null,
            AgentName, superseded);
    }
}
