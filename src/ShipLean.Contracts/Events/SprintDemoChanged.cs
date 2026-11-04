namespace ShipLean.Contracts.Events;

// A sprint's demo changed (every demo write publishes this after commit): open Demo tabs refetch.
public sealed record SprintDemoChanged(Guid ProjectId, Guid SprintId) : ProjectEvent(ProjectId);
