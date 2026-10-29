using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Persisted;

// A project state as TransitionStates sees it. Archived is the state's archived_at being set (a removed state); a live state is not archived.
public sealed record TransitionState(Guid Id, WorkItemTypeId Type, string Name, bool Archived);
