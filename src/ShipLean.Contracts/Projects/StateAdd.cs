using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Projects;

// Id is chosen by the caller so Reorder and Remove in the same request can refer to the new state. IsIntake marks a
// Triage state for GitHub issue intake (state.is_intake), which backlogs leave out unless their state filter names it.
public sealed record StateAdd(Guid Id, WorkItemTypeId Type, string Name, StateCategory Category, bool IsIntake = false);
