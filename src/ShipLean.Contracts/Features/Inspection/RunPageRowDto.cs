namespace ShipLean.Contracts.Inspection;

public sealed record RunPageRowDto(
    string Id,
    string? ParentId,
    int Depth,
    RowKind Kind,
    string Name,
    long StartMs,
    long? EndMs,
    long? DurationMs,
    bool IsError,
    bool IsRetry,
    long? WaitStartMs,
    long? WaitEndMs,
    int CollapsedChildCount,
    bool HasLogs);
