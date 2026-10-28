namespace ShipLean.Contracts.Agents;

// A comment an agent wrote, and the item it is on.
public sealed record AgentComment(Guid Id, Guid WorkItemId, string Body, DateTimeOffset CreatedAt);
