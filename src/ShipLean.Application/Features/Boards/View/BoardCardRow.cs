namespace ShipLean.Application.Features.Boards.View;

// One work item as the board query returns it; Dapper maps the columns onto the constructor by name (Tags is an Array: Npgsql
// reports text[] as one, and it is always a string[]). ColumnId is null only for a card that was never placed.
public sealed record BoardCardRow(
    Guid Id,
    string Key,
    string Title,
    string Type,
    Guid StateId,
    string StateName,
    string StateCategory,
    Guid? AssigneeId,
    string? AssigneeName,
    string? AssigneeInitials,
    string? AssigneeColour,
    string? AssigneePhotoUrl,
    decimal? Points,
    int Priority,
    int? Severity,
    Guid? SprintId,
    string? SprintName,
    Guid? ParentId,
    string? ParentKey,
    string? ParentType,
    string? ParentTitle,
    Array Tags,
    string? ColumnId,
    bool ColumnDone,
    string? LaneId,
    string? ColumnOrder,
    int Revision,
    DateTimeOffset CreatedAt,
    Guid? CreatedById,
    string? CreatedByName,
    DateTimeOffset ChangedAt,
    Guid? ChangedById,
    string? ChangedByName,
    DateTimeOffset StateChangedAt,
    string? OriginalSystem,
    string? OriginalSystemId)
{
    public IReadOnlyList<string> TagNames => [.. (string[])Tags];
}
