using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Persisted;

// From null means any state. A transition whose From or To is no longer a live state is Invalid (derived, never stored).
public sealed record TransitionJson(string Id, WorkItemTypeId Type, StateRefJson? From, StateRefJson To, IReadOnlyList<ActionJson> Actions);
