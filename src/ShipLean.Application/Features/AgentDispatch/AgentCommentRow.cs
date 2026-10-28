namespace ShipLean.Application.Features.AgentDispatch;

internal sealed record AgentCommentRow(Guid Id, Guid? AuthorId, string AuthorName, DateTimeOffset CreatedAt, string Body);
