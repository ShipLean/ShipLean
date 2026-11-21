namespace ShipLean.Contracts.Prompts;

// ClientSource is the AgentSignalSource, camelCase.
public sealed record PromptServing(
    Guid ProjectId,
    PromptKey Key,
    int? Version,
    string BuiltInHash,
    string Agent,
    string? ClientName,
    string? ClientSource,
    string? ItemKey,
    Guid? UserId,
    DateTimeOffset ServedAt);
