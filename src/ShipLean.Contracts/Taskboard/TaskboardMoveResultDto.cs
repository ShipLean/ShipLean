using ShipLean.Contracts.WorkItems;

namespace ShipLean.Contracts.Taskboard;

// ParentId is the task's story or bug after the move (null when it has none). OfferResolve: the move closed the last open
// task of that row and the parent has not reached Resolved, so the row should offer Move to Resolved.
// It describes the destination row only: another row may have become eligible or not, so the GET is the source of truth for every row.
public sealed record TaskboardMoveResultDto(WriteResult Written, Guid? ParentId, bool OfferResolve);
