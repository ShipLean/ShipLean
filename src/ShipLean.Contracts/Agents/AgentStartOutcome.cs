namespace ShipLean.Contracts.Agents;

// Block is null when SessionId was opened. Both are null when the caller's fence refused the start (StartWinnerAsync).
public sealed record AgentStartOutcome(Guid? SessionId, AgentStartBlock? Block);
