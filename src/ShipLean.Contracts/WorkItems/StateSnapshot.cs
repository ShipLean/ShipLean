using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

// A state of one type in one project, as the store and the rules see it. Lists of these are in state order.
public sealed record StateSnapshot(Guid Id, WorkItemTypeId Type, string Name, StateCategory Category);
