namespace ShipLean.Contracts.WorkItems;

// Exactly one of the two is set: the write happened, or the expected revision was stale and nothing was written.
public sealed record WriteOutcome(WriteResult? Written, ConflictDto? Conflict);
