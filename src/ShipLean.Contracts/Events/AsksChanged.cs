namespace ShipLean.Contracts.Events;

// A work item's decisions or questions changed (raised, edited, answered, changed or withdrawn): open work item pages refetch its asks.
public sealed record AsksChanged(Guid ProjectId, Guid WorkItemId) : ProjectEvent(ProjectId);
