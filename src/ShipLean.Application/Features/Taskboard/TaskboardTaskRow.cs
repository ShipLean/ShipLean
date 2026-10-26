using System.Globalization;
using ShipLean.Application.Sprints;
using ShipLean.Core;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Taskboard;

// One task as TaskboardTasks reads it; Dapper maps the columns onto the constructor by name (Tags is an Array: Npgsql reports text[] as one, and it is always a string[]).
internal sealed record TaskboardTaskRow(
    Guid Id,
    string Key,
    string Title,
    decimal? Points,
    DateTimeOffset ChangedAt,
    Guid StateId,
    string StateName,
    string StateCategory,
    Guid? SprintId,
    string? SprintName,
    string? SprintStart,
    string? SprintEnd,
    Guid? AssigneeId,
    string? AssigneeName,
    string? AssigneeInitials,
    string? AssigneeColour,
    string? AssigneePhotoUrl,
    Guid? ParentId,
    string? ParentKey,
    string? ParentType,
    string? ParentTitle,
    Array Tags,
    int Revision)
{
    public StackRowDto ToRow(DateOnly today) => new(
        Id, Key, Title, new TypeRef(WorkItemTypeId.Task, "Task"), new StateRef(StateId, StateName, Enum.Parse<StateCategory>(StateCategory, ignoreCase: true)), Points,
        Parent(),
        SprintId is { } sprint ? new SprintRef(sprint, SprintName!, SprintLabels.Phase(Day(SprintStart!), Day(SprintEnd!), today).ToStatus()) : null,
        AssigneeId is { } assignee ? new UserRef(assignee, AssigneeName!, AssigneeInitials!, AssigneeColour!, AssigneePhotoUrl) : null,
        false, ChangedAt, [.. (string[])Tags], Revision);

    private ParentRefDto? Parent()
    {
        if (ParentId is not { } id)
        {
            return null;
        }

        var type = Enum.Parse<WorkItemTypeId>(ParentType!, ignoreCase: true);
        return new ParentRefDto(id, ParentKey!, new TypeRef(type, type == WorkItemTypeId.Story ? "User Story" : type.ToString()), ParentTitle!);
    }

    private static DateOnly Day(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
