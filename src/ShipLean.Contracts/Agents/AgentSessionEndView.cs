namespace ShipLean.Contracts.Agents;

// What one viewer may see of how a session ended (AgentSessionVisibility.Apply). Every field is null for a live session.
// ShowDeliveries false means the viewer gets an empty delivery list.
public sealed record AgentSessionEndView(string? EndReason, string? EndText, string? EndDetail, AgentRunLogDto? RunLog, bool ShowDeliveries);
