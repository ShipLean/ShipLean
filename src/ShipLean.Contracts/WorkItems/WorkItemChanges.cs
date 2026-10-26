using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.WorkItems;

// The editable fields of a work item, each null (unchanged) or a FieldSet. Links are added and removed, never replaced.
public sealed record WorkItemChanges
{
    public FieldSet<string>? Title { get; init; }

    public FieldSet<string>? Description { get; init; }

    public FieldSet<IReadOnlyList<ChecklistItem>>? AcceptanceCriteria { get; init; }

    public FieldSet<string>? ReproSteps { get; init; }

    public FieldSet<int?>? Severity { get; init; }

    public FieldSet<Guid>? State { get; init; }

    public FieldSet<Guid?>? Assignee { get; init; }

    public FieldSet<Guid?>? Sprint { get; init; }

    public FieldSet<int>? Priority { get; init; }

    public FieldSet<decimal?>? Points { get; init; }

    public FieldSet<Guid?>? Parent { get; init; }

    public FieldSet<IReadOnlyList<string>>? Tags { get; init; }

    // Feature only. Writing StatusLine (even unchanged) or setting ConfirmStatus stamps the status as verified now.
    public FieldSet<string>? Goal { get; init; }

    public FieldSet<string>? StatusLine { get; init; }

    public bool ConfirmStatus { get; init; }

    public IReadOnlyList<LinkValue> AddLinks { get; init; } = [];

    public IReadOnlyList<Guid> RemoveLinkIds { get; init; } = [];
}
