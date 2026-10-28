namespace ShipLean.Contracts.Agents;

// Who a callback token belongs to, without any secret.
public sealed record AgentConnectionIdentity(Guid Id, string Name, string Type);
