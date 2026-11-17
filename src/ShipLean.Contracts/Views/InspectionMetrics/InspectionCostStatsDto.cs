namespace ShipLean.Contracts.Views.InspectionMetrics;

// Tokens over a period, a quarter or the range (CK2). Tasks counts the tasks whose lead time ended there; TasksWithRuns those with
// at least one counted run. TokensPerTask is the mean over the tasks with runs only (a task with no runs is left out, never a zero).
// TokensPerMergedPr is TokensTotal over the merged PRs (the quality wave's count), null when none merged. ChangePctVsPrevious is the
// whole-percent change in TokensPerTask against the previous started sprint, for a single sprint only. Kept is false when the span
// started before the store's KeptSince: its numbers are then null and its labels read "—". Every label is made on the server.
public sealed record InspectionCostStatsDto(
    int Tasks,
    int TasksWithRuns,
    long? TokensPerTask,
    string TokensPerTaskLabel,
    long? TokensTotal,
    string TokensTotalLabel,
    int? MergedPrs,
    long? TokensPerMergedPr,
    string TokensPerMergedPrLabel,
    int? ChangePctVsPrevious,
    bool Kept);
