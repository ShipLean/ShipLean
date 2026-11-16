namespace ShipLean.Contracts.Inspection;

public sealed record RunRow(
    string Id,
    string? ParentId,
    int Depth,
    RowKind Kind,
    string Name,
    DateTimeOffset Start,
    DateTimeOffset? End,
    long? DurationMs,
    bool IsError,
    bool IsRetry,
    DateTimeOffset? WaitStart,
    DateTimeOffset? WaitEnd,
    int CollapsedChildCount,
    IReadOnlyDictionary<string, string> Attributes,
    string? Input,
    string? Output,
    IReadOnlyList<LogRecordData> Logs,
    string? FilePath,
    string? CommitSha);
