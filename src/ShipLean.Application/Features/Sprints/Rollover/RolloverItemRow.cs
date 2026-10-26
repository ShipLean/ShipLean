using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Sprints;

// One unfinished story or bug with its state, assignee and task counts, as the rollover query returns it.
public sealed record RolloverItemRow(
    Guid Id, string Key, string Title, string Type, Guid StateId, string StateName, string StateCategory,
    Guid? AssigneeId, string? AssigneeName, string? AssigneeInitials, string? AssigneeColour, string? AssigneePhotoUrl,
    decimal? Points, int OpenTasks, int TotalTasks)
{
    public RolloverItemDto ToDto(Guid? targetSprintId) => new(
        Id, Key, Title, Enum.Parse<WorkItemTypeId>(Type, true), new StateRef(StateId, StateName, Enum.Parse<StateCategory>(StateCategory, true)),
        AssigneeId is { } assignee ? new UserRef(assignee, AssigneeName!, AssigneeInitials!, AssigneeColour!, AssigneePhotoUrl) : null,
        Points, OpenTasks, TotalTasks, targetSprintId);
}
