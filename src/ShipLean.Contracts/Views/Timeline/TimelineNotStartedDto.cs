using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views.Timeline;

// A task, story or bug of the Feature that never left New and was never closed. Parent is null for a story or bug. Assignee is the
// item's assigned person, null when unassigned; never an agent, since a never-started item has no start move to name one (D3).
public sealed record TimelineNotStartedDto(string Key, WorkItemTypeId Type, string Title, string? Parent, TimelinePersonDto? Assignee);
