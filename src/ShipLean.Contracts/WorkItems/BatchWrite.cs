namespace ShipLean.Contracts.WorkItems;

// One row of a batch: Changes null with a Placement is position only (no revision check, as a reorder).
public sealed record BatchWrite(Guid Id, int ExpectedRevision, WorkItemChanges? Changes, BatchPlacement? Placement);
