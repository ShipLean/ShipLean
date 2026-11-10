using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Builders;

// Arranges one work item of any type with parameterised SQL and returns its id, together with its revision 1 ("created",
// generated the way the seed does it). Further revisions come from RevisionBuilder. Defaults give a valid item: the
// project's first state of the type (lowest order), the next item number from the project, no parent, sprint or
// assignee, Priority 2, and a backlog rank after every seeded item. Parent and state are checked by the schema.
public sealed class WorkItemBuilder(Guid projectId)
{
    private Guid _id = Guid.NewGuid();
    private string _type = "story";
    private string _title = "Arranged work item";
    private Guid? _parentId;
    private Guid? _stateId;
    private string? _stateName;
    private Guid? _sprintId;
    private Guid? _assignee;
    private int _priority = 2;
    private decimal? _points;
    private string[] _tags = [];
    private string? _boardColumnId;
    private bool _boardColumnDone;
    private string? _swimlaneId;
    private string? _columnOrder;
    private string? _backlogOrder;
    private Guid? _createdBy;
    private DateTimeOffset? _createdAt;
    private string? _statusLine;
    private DateTimeOffset? _statusVerifiedAt;
    private Guid? _statusVerifiedBy;
    private string? _statusVerifiedAgent;
    private bool _hasHealth;
    private string? _health;
    private DateTimeOffset? _healthSetAt;
    private Guid? _healthSetBy;
    private string? _healthSetAgent;

    public WorkItemBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    // epic | feature | story | bug | task
    public WorkItemBuilder WithType(string type)
    {
        _type = type;
        return this;
    }

    public WorkItemBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public WorkItemBuilder WithParent(Guid parentId)
    {
        _parentId = parentId;
        return this;
    }

    public WorkItemBuilder WithState(Guid stateId)
    {
        _stateId = stateId;
        return this;
    }

    // A state of the item's type in the project, by name ("Active").
    public WorkItemBuilder WithStateNamed(string name)
    {
        _stateName = name;
        return this;
    }

    public WorkItemBuilder WithSprint(Guid sprintId)
    {
        _sprintId = sprintId;
        return this;
    }

    public WorkItemBuilder WithAssignee(Guid userId)
    {
        _assignee = userId;
        return this;
    }

    public WorkItemBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    public WorkItemBuilder WithPoints(decimal points)
    {
        _points = points;
        return this;
    }

    public WorkItemBuilder WithTags(params string[] tags)
    {
        _tags = tags;
        return this;
    }

    public WorkItemBuilder WithBoardPosition(string columnId, bool done = false, string? swimlaneId = null, string? columnOrder = null)
    {
        _boardColumnId = columnId;
        _boardColumnDone = done;
        _swimlaneId = swimlaneId;
        _columnOrder = columnOrder;
        return this;
    }

    public WorkItemBuilder WithBacklogOrder(string backlogOrder)
    {
        _backlogOrder = backlogOrder;
        return this;
    }

    public WorkItemBuilder WithCreatedBy(Guid userId)
    {
        _createdBy = userId;
        return this;
    }

    public WorkItemBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    // A Feature's status line and when, by whom and through which agent it was last verified (null verifiedAt: never verified).
    // The schema refuses these on any other type.
    public WorkItemBuilder WithStatusLine(string text, DateTimeOffset? verifiedAt, Guid? verifiedBy, string? verifiedAgent)
    {
        _statusLine = text;
        _statusVerifiedAt = verifiedAt;
        _statusVerifiedBy = verifiedBy;
        _statusVerifiedAgent = verifiedAgent;
        return this;
    }

    // A Feature's health (onTrack | atRisk | blocked) and who set it when. The health columns are written only when this is
    // called, so every other insert works on a schema without them.
    public WorkItemBuilder WithHealth(string health, DateTimeOffset setAt, Guid? setBy, string? setAgent)
    {
        _hasHealth = true;
        _health = health;
        _healthSetAt = setAt;
        _healthSetBy = setBy;
        _healthSetAgent = setAgent;
        return this;
    }

    public async Task<Guid> InsertAsync(NpgsqlConnection connection)
    {
        var number = await connection.ExecuteScalarAsync<int>(
            "UPDATE project SET next_item_number = next_item_number + 1 WHERE id = @projectId RETURNING next_item_number - 1",
            new { projectId });
        await connection.ExecuteAsync(
            """
            INSERT INTO work_item (id, project_id, number, key, type, title, state_id, assigned_to, sprint_id, priority, story_points,
                                   backlog_order, parent_id, parent_type, tags, board_column_id, board_column_done, swimlane_id, column_order,
                                   created_by, created_at, status_line, status_verified_at, status_verified_by, status_verified_agent)
            SELECT @id, project.id, @number, project.key || '-' || @number, @type, @title,
                   coalesce(@stateId::uuid, (SELECT state.id FROM state
                                       WHERE state.project_id = project.id AND state.type = @type
                                         AND (@stateName::text IS NULL OR state.name = @stateName::text)
                                       ORDER BY state.sort_order LIMIT 1)),
                   @assignee::uuid, @sprintId::uuid, @priority, @points::numeric,
                   coalesce(@backlogOrder::text, '9' || lpad(@number::text, 7, '0')),
                   @parentId::uuid, (SELECT parent.type FROM work_item parent WHERE parent.id = @parentId::uuid),
                   @tags::text[], @boardColumnId::text, @boardColumnDone, @swimlaneId::text, @columnOrder::text,
                   @createdBy::uuid, coalesce(@createdAt::timestamptz, now()), @statusLine::text, @statusVerifiedAt::timestamptz,
                   @statusVerifiedBy::uuid, @statusVerifiedAgent::text
            FROM project WHERE project.id = @projectId
            """,
            new
            {
                id = _id,
                projectId,
                number,
                type = _type,
                title = _title,
                stateId = _stateId,
                stateName = _stateName,
                assignee = _assignee,
                sprintId = _sprintId,
                priority = _priority,
                points = _points,
                backlogOrder = _backlogOrder,
                parentId = _parentId,
                tags = _tags,
                boardColumnId = _boardColumnId,
                boardColumnDone = _boardColumnDone,
                swimlaneId = _swimlaneId,
                columnOrder = _columnOrder,
                createdBy = _createdBy,
                createdAt = _createdAt?.ToUniversalTime(),
                statusLine = _statusLine,
                statusVerifiedAt = _statusVerifiedAt?.ToUniversalTime(),
                statusVerifiedBy = _statusVerifiedBy,
                statusVerifiedAgent = _statusVerifiedAgent,
            });
        if (_hasHealth)
        {
            await connection.ExecuteAsync(
                """
                UPDATE work_item SET health = @health, health_set_at = @setAt, health_set_by = @setBy::uuid, health_set_agent = @setAgent::text
                WHERE id = @id
                """,
                new { id = _id, health = _health, setAt = _healthSetAt?.ToUniversalTime(), setBy = _healthSetBy, setAgent = _healthSetAgent });
        }
        await connection.ExecuteAsync(
            """
            INSERT INTO revision (work_item_id, rev, changed_by, changed_at, changes)
            SELECT w.id, 1, w.created_by, w.changed_at,
                   (SELECT jsonb_agg(jsonb_build_object('field', f.field, 'from', f.from_value, 'to', f.to_value,
                                                        'fromLabel', NULL::text, 'toLabel', f.to_label) ORDER BY f.ord)
                    FROM (VALUES
                        (1, 'title', NULL::jsonb, to_jsonb(w.title), NULL::text, true),
                        (2, 'type', NULL, to_jsonb(w.type), NULL, true),
                        (3, 'state', NULL, to_jsonb(w.state_id), state.name, true),
                        (4, 'assignee', NULL, to_jsonb(w.assigned_to), person.name, w.assigned_to IS NOT NULL),
                        (5, 'priority', NULL, to_jsonb(w.priority), NULL, true),
                        (6, 'points', NULL, to_jsonb(trim_scale(w.story_points)), NULL, w.story_points IS NOT NULL),
                        (7, 'tags', '[]'::jsonb, to_jsonb(w.tags), NULL, cardinality(w.tags) > 0),
                        (8, 'sprint', NULL, to_jsonb(w.sprint_id), sprint.name, w.sprint_id IS NOT NULL),
                        (9, 'parent', NULL, to_jsonb(w.parent_id), parent.title, w.parent_id IS NOT NULL)
                    ) AS f (ord, field, from_value, to_value, to_label, included)
                    WHERE f.included)
            FROM work_item w
            JOIN state ON state.id = w.state_id
            LEFT JOIN app_user person ON person.id = w.assigned_to
            LEFT JOIN sprint ON sprint.id = w.sprint_id
            LEFT JOIN work_item parent ON parent.id = w.parent_id
            WHERE w.id = @id
            """,
            new { id = _id });
        return _id;
    }
}
