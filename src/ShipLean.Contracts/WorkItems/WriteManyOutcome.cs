namespace ShipLean.Contracts.WorkItems;

// All or nothing; exactly one of the two is set. Conflicts lists every stale item, in input order, and then nothing was written.
public sealed record WriteManyOutcome(IReadOnlyList<WriteResult>? Written, IReadOnlyList<ConflictDto>? Conflicts);
