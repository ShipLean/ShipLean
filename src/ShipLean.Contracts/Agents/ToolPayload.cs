namespace ShipLean.Contracts.Agents;

// An uploaded payload as the tool call reads it: the bytes exactly as uploaded (a JSON object, or raw text for Field).
public sealed record ToolPayload(string Tool, string? Field, byte[] Body);
