namespace ShipLean.Contracts.Agents;

// The agent connection a report comes from: its id (every write is scoped to that connection's sessions) and its name (the
// agent stamp on the comments it writes).
public sealed record AgentReporter(Guid ConnectionId, string Name);
