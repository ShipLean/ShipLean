namespace ShipLean.Contracts.Agents;

// The body of creating or editing a connection. The receiver URL is https, or http on localhost.
public sealed record AgentConnectionPut(string Name, string ReceiverUrl);
