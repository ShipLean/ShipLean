using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Projects;

// Id is chosen by the caller so Reorder and Remove in the same request can refer to the new state.
public sealed record StateAdd(Guid Id, WorkItemTypeId Type, string Name, StateCategory Category);
