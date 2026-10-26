namespace ShipLean.Contracts.Views;

// Parent is the Story or Bug header card; null for the unparented row, which is always last. The row's task counts are
// Parent.Rollups for the task type (the unparented row counts its cells). OfferResolve: every task is closed and the parent has
// not reached Resolved. Cells has one entry per column, in column order.
public sealed record TaskboardRowDto(CardDto? Parent, bool OfferResolve, IReadOnlyList<TaskboardCellDto> Cells);
