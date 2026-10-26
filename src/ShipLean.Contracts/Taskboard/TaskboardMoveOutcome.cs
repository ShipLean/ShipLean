using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Taskboard;

// Exactly one of the two is set, as for WriteOutcome: the move happened, or the expected revision was stale.
public sealed record TaskboardMoveOutcome(TaskboardMoveResultDto? Written, ConflictDto? Conflict);
