using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Backlog;

// A stale item is not an error: it comes back in Stale and every other item is written. Both lists are in request order.
public sealed record BulkWriteResult(IReadOnlyList<WriteResult> Written, IReadOnlyList<ConflictDto> Stale);
