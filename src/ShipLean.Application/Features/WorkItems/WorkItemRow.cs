using System.Text.Json;
using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

// One work_item row as Select returns it; Dapper maps the columns onto the constructor by name.
internal sealed record WorkItemRow(
    Guid Id,
    Guid ProjectId,
    int Number,
    string Key,
    string Type,
    string Title,
    string Description,
    string AcceptanceCriteria,
    string ReproSteps,
    int? Severity,
    Guid StateId,
    Guid? AssigneeId,
    Guid? SprintId,
    int Priority,
    decimal? Points,
    string BacklogOrder,
    Guid? ParentId,
    string? ParentType,
    string TagsJson,
    string? ColumnId,
    bool ColumnDone,
    string? LaneId,
    string? ColumnOrder,
    string? OriginalSystem,
    string? OriginalSystemId,
    string? OriginalUrl,
    int Revision,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? ChangedBy,
    DateTimeOffset ChangedAt,
    DateTimeOffset StateChangedAt,
    string Goal,
    string StatusLine,
    DateTimeOffset? StatusVerifiedAt,
    Guid? StatusVerifiedBy,
    string? StatusVerifiedAgent,
    string? StatusVerifiedVia,
    string? Slug,
    string? StartedOnText)
{
    public const string Select =
        """
        SELECT w.id, w.project_id AS projectId, w.number, w.key, w.type, w.title, w.description,
               w.acceptance_criteria::text AS acceptanceCriteria, w.repro_steps AS reproSteps, w.severity::int AS severity,
               w.state_id AS stateId, w.assigned_to AS assigneeId, w.sprint_id AS sprintId, w.priority::int AS priority,
               w.story_points AS points, w.backlog_order AS backlogOrder, w.parent_id AS parentId, w.parent_type AS parentType,
               to_jsonb(w.tags)::text AS tagsJson, w.board_column_id AS columnId, w.board_column_done AS columnDone, w.swimlane_id AS laneId,
               w.column_order AS columnOrder, w.original_system AS originalSystem, w.original_system_id AS originalSystemId,
               w.original_url AS originalUrl, w.revision, w.created_by AS createdBy, w.created_at AS createdAt,
               w.changed_by AS changedBy, w.changed_at AS changedAt, w.state_changed_at AS stateChangedAt,
               COALESCE(w.goal, '') AS goal, COALESCE(w.status_line, '') AS statusLine, w.status_verified_at AS statusVerifiedAt,
               w.status_verified_by AS statusVerifiedBy, w.status_verified_agent AS statusVerifiedAgent, w.status_verified_via AS statusVerifiedVia,
               w.slug, to_char(w.started_on, 'YYYY-MM-DD') AS startedOnText
        FROM work_item w
        """;

    public IReadOnlyList<string> Tags => JsonSerializer.Deserialize<string[]>(TagsJson)!;

    public IReadOnlyList<ChecklistItem> Criteria => JsonSerializer.Deserialize<List<ChecklistItem>>(AcceptanceCriteria, PersistedJson.Options)!;

    public OriginalSystemDto? Original => OriginalSystem is null ? null : new OriginalSystemDto(OriginalSystem, OriginalSystemId!, OriginalUrl);

    public DateOnly? StartedOn => StartedOnText is null ? null : DateOnly.ParseExact(StartedOnText, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public WorkItemSnapshot ToSnapshot(IReadOnlyList<LinkSnapshot> links) => new(
        Id, ProjectId, Number, Key, WorkItemTypes.Parse(Type), Title, Description, Criteria, ReproSteps, Severity, StateId, AssigneeId,
        SprintId, Priority, WorkItemPoints.Normalise(Points), BacklogOrder, ParentId, ParentType is null ? null : WorkItemTypes.Parse(ParentType),
        Tags, ColumnId, ColumnDone, LaneId, ColumnOrder, Original, Revision, CreatedBy, CreatedAt, ChangedBy, ChangedAt, StateChangedAt, links,
        Goal, StatusLine, StatusVerifiedAt, StatusVerifiedBy, StatusVerifiedAgent, StatusVerifiedVia, Slug, StartedOn);
}
