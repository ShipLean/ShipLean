namespace ShipLean.Application.Features.AgentDispatch;

internal sealed record AgentPromptCommentRow(Guid WorkItemId, Guid? AuthorId, string? AgentName, string Body, DateTimeOffset CreatedAt);
