namespace ShipLean.Contracts.Sprints;

public sealed record SprintMetricsDto(Guid SprintId, decimal Committed, decimal Completed, decimal Remaining, IReadOnlyList<BurndownPoint> Burndown);
