using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Projects;

// The full new order of one type's live states.
public sealed record StateReorder(WorkItemTypeId Type, IReadOnlyList<Guid> StateIds);
