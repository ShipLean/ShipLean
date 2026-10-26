namespace ShipLean.WebApi.Features.Mcp.Planning.Reports;

// The free-text fields of a report_task call, as the report comment shows them.
internal sealed record ReportFields(
    IReadOnlyList<string> TestsRun,
    IReadOnlyList<string> AcceptanceTests,
    string? ConfigFilesChanged,
    string? RedTests,
    int? ContextUsedK,
    string? PublicSurface,
    string? Decisions,
    string? Gotchas,
    IReadOnlyList<string> LeftUndone,
    string? TemporaryBehaviour,
    string? ConstraintNotMet);
