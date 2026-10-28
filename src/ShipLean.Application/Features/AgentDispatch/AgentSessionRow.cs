namespace ShipLean.Application.Features.AgentDispatch;

internal sealed record AgentSessionRow(Guid Id, Guid ConnectionId, Guid CreatedBy, DateTimeOffset CreatedAt, string RepoUrl, string BaseBranch);
