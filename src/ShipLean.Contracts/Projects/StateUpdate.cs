using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Projects;

// Renames and/or recategorises a live state; it keeps its id and its items.
public sealed record StateUpdate(Guid StateId, string Name, StateCategory Category);
