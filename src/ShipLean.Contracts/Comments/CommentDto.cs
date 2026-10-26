using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Comments;

// One comment on a work item. Author is null for an imported comment whose author has no user. EditedAt and EditedLabel
// are null until the comment is edited. AgentName is the agent that wrote it for Author ("Claude Code (claude.ai)"), otherwise null.
// Superseded is null until any member marks the comment superseded (a soft flag; the comment stays). Archived comments are never returned.
public sealed record CommentDto(
    Guid Id,
    Guid WorkItemId,
    UserRef? Author,
    string Body,
    string BodyHtml,
    DateTimeOffset CreatedAt,
    string CreatedLabel,
    DateTimeOffset? EditedAt,
    string? EditedLabel,
    string? AgentName = null,
    CommentSuperseded? Superseded = null);
