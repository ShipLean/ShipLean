namespace ShipLean.Contracts.Agents;

// The run log an agent uploaded for its session (an attachment on the item). Url is the attachment download route.
public sealed record AgentRunLogDto(Guid AttachmentId, string Name, long SizeBytes, string Url);
