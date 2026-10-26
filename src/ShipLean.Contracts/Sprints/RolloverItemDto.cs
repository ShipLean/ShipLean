using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Sprints;

// One unfinished story or bug on the rollover screen. OpenTasks and TotalTasks count its tasks.
public sealed record RolloverItemDto(
    Guid WorkItemId, string Key, string Title, WorkItemTypeId Type, StateRef State, UserRef? Assignee, decimal? Points,
    int OpenTasks, int TotalTasks, Guid? DefaultTargetSprintId);
