namespace ShipLean.Contracts.Inspection;

// The paged, errors-first rows of a run, for the MCP get_run tool.
public sealed record RunRowsPage(
    RunHeaderDto Header,
    bool LogOnly,
    string? Banner,
    bool Truncated,
    string? GrafanaUrl,
    bool ContentShown,
    int TotalRows,
    int ErrorRows,
    string? NextCursor,
    IReadOnlyList<RunRowsPageRow> Rows);

public sealed record RunRowsPageRow(int Turn, RunPageRowDto Row);
