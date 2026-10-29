using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

// The full after-image of one item as the store is about to write it: every editable field plus the board position and
// the backlog order, so a rule can amend any of them. ColumnOrder and BacklogOrder are already the values the store
// computed for this write (the new column order on a move, the top of the backlog on create); the store writes whatever
// the rules return. If the rules change ColumnId but leave ColumnOrder as proposed, the store puts the item last in the new
// column. Bulk marks a write that moves many items at once on a person's behalf (a settings save, a rollover); rules that mean "a person started this" skip it, and the store never writes it. Reorder doesn't call the rules (it changes no field). WorkItemId is the id the item has (or will have, on create).
public sealed record ProposedChange(
    Guid WorkItemId,
    Guid ProjectId,
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
    string? ColumnId,
    bool ColumnDone,
    string? LaneId,
    string? ColumnOrder,
    string BacklogOrder,
    string Goal = "",
    string StatusLine = "",
    DateTimeOffset? StatusVerifiedAt = null,
    bool Bulk = false);
