using System.Text.Json.Serialization;

namespace ShipLean.Contracts.WorkItems;

// A card dropped on a board: the column (and its Doing/Done half), optionally a lane, placed before another card of that
// column (null = last). A null ColumnId is a Taskboard drop: the columns there are task states, so the state comes from
// Set.State and the card is ordered by column order within its (state, parent) cell. Set carries field writes that belong to the same drop; they share the one revision.
public sealed record MoveTarget([property: JsonRequired] int ExpectedRevision, string? ColumnId, bool Done, string? LaneId, Guid? BeforeItemId, WorkItemChanges? Set);
