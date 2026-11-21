namespace ShipLean.Contracts.Prompts;

// Versions are newest first.
public sealed record PromptHistoryDto(PromptKey Key, IReadOnlyList<PromptVersionDto> Versions, string? BaseLabel);
