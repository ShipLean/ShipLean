namespace ShipLean.Contracts.WorkItems;

// Where the card sits on its project's board for the item's level. ColumnId is null until the item has been placed.
public sealed record BoardPositionDto(string? ColumnId, bool Done, string? LaneId, string? ColumnOrder);
