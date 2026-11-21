namespace ShipLean.Contracts.Prompts;

public sealed record PromptSavedDto(PromptKey Key, PromptStatus Status, int? InUseVersion);
