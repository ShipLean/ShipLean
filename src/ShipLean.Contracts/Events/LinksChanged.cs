namespace ShipLean.Contracts.Events;

// A work item's links changed (a link added, edited or archived, or a card's state or preview refreshed): open work item pages refetch its link cards.
public sealed record LinksChanged(Guid ProjectId, Guid WorkItemId) : ProjectEvent(ProjectId);
