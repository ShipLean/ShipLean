using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

// Everything the work item form, backlog and board cards show for one item. Markdown fields come with their rendered HTML.
// Goal, StatusLine, StatusVerified, Slug and StartedOn are a Feature's: Goal and StatusLine are "" and the rest null on other types.
public sealed record WorkItemDto(
    Guid Id,
    Guid ProjectId,
    string Key,
    int Number,
    TypeRef Type,
    string Title,
    string Description,
    string DescriptionHtml,
    IReadOnlyList<ChecklistItem> AcceptanceCriteria,
    string ReproSteps,
    string ReproStepsHtml,
    int? Severity,
    StateRef State,
    UserRef? Assignee,
    UserRef? CreatedBy,
    UserRef? ChangedBy,
    SprintRef? Sprint,
    int Priority,
    decimal? Points,
    ParentRefDto? Parent,
    IReadOnlyList<string> Tags,
    BoardPositionDto Position,
    string BacklogOrder,
    IReadOnlyList<LinkDto> Links,
    IReadOnlyList<ChildSummaryDto> Children,
    OriginalSystemDto? OriginalSystem,
    int Revision,
    DateTimeOffset CreatedAt,
    string CreatedAtLabel,
    DateTimeOffset ChangedAt,
    string ChangedAtLabel,
    string Goal,
    string StatusLine,
    StatusVerifiedDto? StatusVerified,
    string? Slug,
    DateOnly? StartedOn,
    string? StartedLabel);
