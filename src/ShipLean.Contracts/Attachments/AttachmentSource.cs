namespace ShipLean.Contracts.Attachments;

// Who attached a file when it was not a person in the browser: the agent's name and the MCP tool it used.
public sealed record AttachmentSource(string AgentName, string Via);
