namespace ShipLean.Contracts.Views.InspectionMetrics;

// How many of the range's measured tasks ended at a commit time. The rest ended at Resolved and read longer (the commit-time
// backfill has not run for them); below 70% the page shows a banner. Percent is null with no tasks.
public sealed record InspectionCoverageDto(int Tasks, int WithCommitTime, int? Percent);
