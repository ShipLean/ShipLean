namespace ShipLean.Contracts.Prompts;

// Custom is the version in use, else the newest. SectionErrors is the number of agent-section errors in the text in use.
// Update is set only when Status is BuiltInUpdated.
public sealed record PromptDto(
    PromptKey Key,
    string File,
    string Name,
    string WhenRuns,
    string ServedBy,
    string ReadBy,
    string? Note,
    IReadOnlyList<string> Placeholders,
    IReadOnlyList<string> InsertedParts,
    PromptStatus Status,
    string BuiltInText,
    string BuiltInHash,
    PromptCustomDto? Custom,
    string? YourChangesPatch,
    PromptUpdateDto? Update,
    int SectionErrors);
