namespace ShipLean.Contracts.Prompts;

public sealed record AgentSignals(string? McpClientName, string? Originator, string? UserAgent, string? OAuthClientName);
