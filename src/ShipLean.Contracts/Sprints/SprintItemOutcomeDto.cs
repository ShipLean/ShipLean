using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Sprints;

// What one story or bug did in a sprint, by the rules ISprintMetrics uses for committed and completed.
// Committed: in the sprint at 00:00 UTC on its first day. AddedMidSprint: not committed, joined during the sprint and still in
// it at the end (or when its last working day began). Completed: counted in the sprint's completed points. CarriedOver: in the
// sprint when it ended, or moved out during its last working day, and neither completed for it nor removed. An item is listed when any flag is set.
public sealed record SprintItemOutcomeDto(
    Guid Id, string Key, string Title, WorkItemTypeId Type, decimal? Points,
    bool Committed, bool AddedMidSprint, bool Completed, bool CarriedOver);
