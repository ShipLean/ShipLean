using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Boards;

// The project's states of one type, in state order, Removed included: the rows of the Columns matrix.
public sealed record BoardTypeStatesDto(WorkItemTypeId Type, IReadOnlyList<StateRef> States);
