namespace ShipLean.Contracts.WorkItems;

// Written and Stale are in input order. A stale row was left out; every other row was written.
public sealed record BatchWriteOutcome(IReadOnlyList<WriteResult> Written, IReadOnlyList<ConflictDto> Stale);
