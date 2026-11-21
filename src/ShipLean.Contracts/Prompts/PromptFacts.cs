namespace ShipLean.Contracts.Prompts;

public sealed record PromptFacts(
    PromptKey Key,
    PromptStage Stage,
    string File,
    string Name,
    string WhenRuns,
    string ServedBy,
    string ReadBy,
    string? Note,
    IReadOnlyList<string> Placeholders,
    IReadOnlyList<string> InsertedParts);
