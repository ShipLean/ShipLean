namespace ShipLean.Contracts.Prompts;

public sealed record PromptSaveDto(string Text, string Note, PromptVersionSource Source);
