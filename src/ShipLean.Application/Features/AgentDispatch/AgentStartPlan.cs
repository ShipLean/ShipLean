using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

// What a session would start with, or why it cannot. Block is null when it can; Connection, RepoUrl and BaseBranch are then set.
public sealed record AgentStartPlan(AgentStartBlock? Block, Guid ConnectionId, string RepoUrl, string BaseBranch, string? Prompt = null);
