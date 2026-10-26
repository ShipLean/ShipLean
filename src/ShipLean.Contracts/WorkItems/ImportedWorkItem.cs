using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

// A work item as the importer resolved it: ids instead of refs, the source system's key number kept, and its changelog.
public sealed record ImportedWorkItem(
    OriginalSystemDto OriginalSystem,
    int KeyNumber,
    WorkItemTypeId Type,
    string Title,
    string Description,
    IReadOnlyList<ChecklistItem> AcceptanceCriteria,
    string ReproSteps,
    int? Severity,
    Guid StateId,
    Guid? AssigneeId,
    Guid? SprintId,
    int Priority,
    decimal? Points,
    Guid? ParentId,
    IReadOnlyList<string> Tags,
    IReadOnlyList<LinkValue> Links,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    Guid ChangedBy,
    DateTimeOffset ChangedAt,
    DateTimeOffset StateChangedAt,
    IReadOnlyList<ImportedRevision> Revisions);
