namespace ShipLean.Contracts.Events;

// ColumnId is null for a Taskboard drop (its columns are task states); ColumnOrder is then the order within the (state, parent) cell.
// ColumnOrder is null when the card left its board.
public sealed record WorkItemMoved(
    Guid ProjectId, Guid Id, int Revision, string? ColumnId, bool Done, string? LaneId, string? ColumnOrder, Guid StateId, Guid? ParentId)
    : ProjectEvent(ProjectId);
