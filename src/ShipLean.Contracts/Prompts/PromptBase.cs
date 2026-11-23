namespace ShipLean.Contracts.Prompts;

// The version a caller read as in use before saving (C2): a custom version number, or null for the built-in.
public sealed record PromptBase(int? Version);
