namespace ShipLean.Contracts.Agents;

// What create_payload_upload asks for. Field null: the body is a JSON object of the tool's arguments; otherwise the body is
// the raw UTF-8 text of that one string argument. AuthorizationId is the agent's OAuth authorization, null for a caller that
// has none (the gateway). AgentStamp is the agent as it was then (AgentStamp.Of).
public sealed record NewToolPayload(string Tool, string? Field, Guid UserId, Guid? AuthorizationId, string AgentStamp);
