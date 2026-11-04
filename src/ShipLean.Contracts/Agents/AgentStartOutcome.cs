namespace ShipLean.Contracts.Agents;

// Block is null when SessionId was opened.
public sealed record AgentStartOutcome(Guid? SessionId, AgentStartBlock? Block);
