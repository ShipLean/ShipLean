namespace ShipLean.Contracts.Backlog;

// 1 to BulkLimits.MaxRows rows, counting tasks. SprintId applies to every item and its tasks. A null Placement means first.
public sealed record BacklogAddRequest(IReadOnlyList<BacklogAddItem> Items, Guid? SprintId, BulkPlacement? Placement);
