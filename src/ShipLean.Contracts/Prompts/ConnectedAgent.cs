namespace ShipLean.Contracts.Prompts;

public sealed record ConnectedAgent(string Id, AgentSignalSource Source, string? Raw);
