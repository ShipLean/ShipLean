namespace ShipLean.Contracts.Taskboard;

// The row a task was dropped on. Target is the row's story or bug (Stories; null = the Unparented row) or its person
// (People; null = the Unassigned row).
public sealed record TaskboardMoveRow(TaskboardGrouping Grouping, Guid? Target);
