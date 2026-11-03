using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Backlog;

// Each item, then its tasks, in input order.
public sealed record BacklogAddResult(IReadOnlyList<WriteResult> Created);
