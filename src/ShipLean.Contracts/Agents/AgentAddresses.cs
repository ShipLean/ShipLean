namespace ShipLean.Contracts.Agents;

// What the current request calls the server: the authorization server's issuer and the /mcp resource tokens are bound to.
public sealed record AgentAddresses(string Issuer, string Resource);
