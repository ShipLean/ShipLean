namespace ShipLean.Contracts.Prompts;

public sealed record PromptVersionDto(int Number, string Note, string By, string AtLabel, bool InUse, PromptVersionSource Source, string Text);
