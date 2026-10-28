namespace ShipLean.Contracts.Agents;

// A link the agent attached to its session (a pull request, a branch).
public sealed record AgentUrlDto(string Label, string Url);
