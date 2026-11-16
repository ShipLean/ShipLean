namespace ShipLean.Contracts.Sprints;

// WorkingDays is every working day of the sprint, first to last (the burndown's x-axis), on the sprint's UTC calendar days
// like BurndownPoint (SHIP-1702 D8): the client places the chart by it and does no date arithmetic of its own.
public sealed record SprintMetricsDto(Guid SprintId, decimal Committed, decimal Completed, decimal Remaining, IReadOnlyList<BurndownPoint> Burndown, IReadOnlyList<DateOnly> WorkingDays);
