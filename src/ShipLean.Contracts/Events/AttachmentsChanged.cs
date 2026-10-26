namespace ShipLean.Contracts.Events;

// A work item's attachments changed (added or archived): open work item pages refetch its attachments.
public sealed record AttachmentsChanged(Guid ProjectId, Guid WorkItemId) : ProjectEvent(ProjectId);
