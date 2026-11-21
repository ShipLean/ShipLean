namespace ShipLean.Contracts.Prompts;

public sealed record PromptUpdateDto(string ChangedLabel, string BaseLabel, string Patch, bool AppliesCleanly, string MergedText);
