namespace ShipLean.Contracts.Prompts;

// A check that doesn't stop a save; Message matches the editor's wording (promptChecks.ts).
public sealed record PromptWarningDto(PromptWarningKind Kind, string Message, string? Placeholder, string? Tag, int? Line);
