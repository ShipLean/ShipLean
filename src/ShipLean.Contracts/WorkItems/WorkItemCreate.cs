using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

// A new item starts in its type's first Proposed state; Changes, when given, are applied on top in the same revision.
public sealed record WorkItemCreate(Guid ProjectId, WorkItemTypeId Type, string Title, Guid? ParentId, Guid? SprintId, WorkItemChanges? Changes)
{
    // Feature only. Slug is set here and read-only afterwards; StartedOn defaults to the creation day in UTC.
    public string Goal { get; init; } = "";

    public string StatusLine { get; init; } = "";

    public string? Slug { get; init; }

    public DateOnly? StartedOn { get; init; }
}
