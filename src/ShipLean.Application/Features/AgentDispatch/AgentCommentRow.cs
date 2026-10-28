namespace ShipLean.Application.Features.AgentDispatch;

internal sealed record AgentCommentRow(Guid Id, Guid? AuthorId, string AuthorName, string AuthorEmail, DateTimeOffset CreatedAt, string Body);
