namespace ShipLean.Contracts.Agents;

// The session Start agent opened. State is always "submitted"; its `created` webhook is queued.
public sealed record AgentSessionStartedDto(Guid SessionId, string State);
