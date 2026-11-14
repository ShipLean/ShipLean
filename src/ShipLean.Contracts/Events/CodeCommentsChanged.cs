namespace ShipLean.Contracts.Events;

// A code comment on a Task or Bug was added, replied to, resolved, marked outdated or sent back: open Changes pages refetch.
public sealed record CodeCommentsChanged(Guid ProjectId, Guid WorkItemId) : ProjectEvent(ProjectId);
