using System.Globalization;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItems.Reads.Tree;

// One row of TreeSql.ItemColumns; Dapper maps the columns onto the constructor by name (Tags is an Array: Npgsql reports text[] as one, and it is always a string[]).
internal sealed record ItemRow(
    Guid Id,
    string Key,
    string Title,
    string Type,
    decimal? Points,
    DateTimeOffset ChangedAt,
    bool HasAcceptanceCriteria,
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
    int Revision,
    int Depth)
{
    public WorkTreeItemDto ToItem(DateOnly today) => new(Id, Key, Title, TypeRef(), State(), Sprint(today), Points, Assignee(), ParentId, Depth, Revision);

    public StackRowDto ToStackRow(DateOnly today) => new(Id, Key, Title, TypeRef(), State(), Points, Parent(), Sprint(today), Assignee(), HasAcceptanceCriteria, ChangedAt, [.. (string[])Tags], Revision);

    private TypeRef TypeRef() => WorkItemTypes.Ref(WorkItemTypes.Parse(Type));

    private StateRef State() => new(StateId, StateName, Enum.Parse<StateCategory>(StateCategory, ignoreCase: true));

    private SprintRef? Sprint(DateOnly today)
        => SprintId is { } id
            ? new SprintRef(id, SprintName!, SprintLabels.Phase(Day(SprintStart!), Day(SprintEnd!), today).ToStatus())
            : null;

    private UserRef? Assignee() => AssigneeId is { } id ? new UserRef(id, AssigneeName!, AssigneeInitials!, AssigneeColour!, AssigneePhotoUrl) : null;

    private ParentRefDto? Parent()
        => ParentId is { } id ? new ParentRefDto(id, ParentKey!, WorkItemTypes.Ref(WorkItemTypes.Parse(ParentType!)), ParentTitle!) : null;

    private static DateOnly Day(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
