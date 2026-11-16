namespace ShipLean.Contracts.Events;

// A Task or Bug's commits changed (commits added): open Changes sections refetch, and an open Changes page offers a reload.
public sealed record CommitsChanged(Guid ProjectId, Guid WorkItemId) : ProjectEvent(ProjectId);
