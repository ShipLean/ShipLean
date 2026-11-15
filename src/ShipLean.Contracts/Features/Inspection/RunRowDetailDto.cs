namespace ShipLean.Contracts.Inspection;

// GET .../runs/{runId}/rows/{rowId}. Input and Output are the first 40 lines; null with ContentHidden when content is off.
public sealed record RunRowDetailDto(
    RowKind Kind,
    string Name,
    string StartLabel,
    long DurationMs,
    long OffsetMs,
    long? WaitMs,
    bool ContentHidden,
    string? Input,
    string? Output,
    bool OutputTruncated,
    IReadOnlyList<RunAttributeDto> Attributes,
    IReadOnlyList<RunLogDto> Logs,
    string? FilePath,
    string? CommitSha,
    string? GrafanaUrl);
