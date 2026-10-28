namespace ShipLean.Application.Features.AgentDispatch.Rendering;

public sealed record AgentCommentInput(string Id, AgentUserInput Author, DateTimeOffset CreatedAt, string Body);
