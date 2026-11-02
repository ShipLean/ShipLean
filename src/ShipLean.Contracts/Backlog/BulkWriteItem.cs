using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Backlog;

public sealed record BulkWriteItem(Guid Id, int ExpectedRevision, WorkItemChanges? Changes, BulkPlacement? Placement);
