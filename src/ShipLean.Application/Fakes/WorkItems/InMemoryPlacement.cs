namespace ShipLean.Application.Fakes.WorkItems;

// Where a move puts the card. A null column is a Taskboard cell; a null lane leaves the lane as it was.
internal sealed record InMemoryPlacement(string? ColumnId, bool Done, string? LaneId, Guid? BeforeItemId);
