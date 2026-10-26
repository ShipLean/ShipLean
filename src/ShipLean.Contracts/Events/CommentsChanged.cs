namespace ShipLean.Contracts.Events;

// A work item's discussion changed (added, edited or archived): open work item pages refetch its comments.
public sealed record CommentsChanged(Guid ProjectId, Guid WorkItemId) : ProjectEvent(ProjectId);
