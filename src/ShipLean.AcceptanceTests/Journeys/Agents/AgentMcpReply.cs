using System.Net;

namespace ShipLean.AcceptanceTests.Journeys.Agents;

// What a raw call to /mcp answered: the status and the WWW-Authenticate challenge.
public sealed record AgentMcpReply(HttpStatusCode Status, string WwwAuthenticate);
