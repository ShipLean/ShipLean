namespace ShipLean.Application.Features.Timeline.Facts.Sql;

// The SQL of the facts reader: one query per kind of fact for the whole tree.
internal static class TimelineFactsSql
{
    // The Feature, its stories and bugs, and their tasks, with current state. Tags are read for the stories' wave: tags.
    public const string Items =
        """
        SELECT w.id, w.key, w.type, w.title, w.parent_id AS parentId, w.assigned_to AS assigneeId, w.created_at AS createdAt,
               w.created_by AS createdBy, w.tags, s.name AS stateName, s.category AS stateCategory
        FROM work_item w
        JOIN state s ON s.id = w.state_id
        WHERE w.project_id = @projectId
          AND (w.id = @featureId
               OR w.parent_id = @featureId
               OR w.parent_id IN (SELECT c.id FROM work_item c WHERE c.parent_id = @featureId AND c.project_id = @projectId))
          AND w.type IN ('feature', 'story', 'bug', 'task')
        """;

    public const string ProjectKeyAndEpic =
        """
        SELECT p.key AS projectKey, e.key AS epicKey, e.title AS epicTitle
        FROM project p
        JOIN work_item f ON f.project_id = p.id AND f.id = @featureId
        LEFT JOIN work_item e ON e.id = f.parent_id AND e.type = 'epic'
        WHERE p.id = @projectId
        """;

    // Every state move, revision 1 included, with the target state's name and category.
    public const string Moves =
        """
        SELECT w.key AS itemKey, r.changed_at AS at, r.rev, s.name AS stateName, s.category, r.agent_name AS agentName, r.changed_by AS userId
        FROM revision r
        JOIN work_item w ON w.id = r.work_item_id
        CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c(value)
        JOIN state s ON s.id::text = (c.value -> 'to') #>> '{}'
        WHERE r.work_item_id = ANY(@ids) AND c.value ->> 'field' = @stateField
        ORDER BY r.changed_at, r.rev
        """;

    // Every tags change on a task after which the Blocked tag (any case) is in the set when it was not, or the reverse.
    public const string BlockedTags =
        """
        SELECT key AS itemKey, at, rev, after AS blockedAfter
        FROM (
            SELECT w.key, r.changed_at AS at, r.rev,
                   CASE WHEN jsonb_typeof(c.value -> 'from') = 'array'
                        THEN EXISTS (SELECT 1 FROM jsonb_array_elements_text(c.value -> 'from') t WHERE lower(t) = 'blocked') ELSE false END AS before,
                   CASE WHEN jsonb_typeof(c.value -> 'to') = 'array'
                        THEN EXISTS (SELECT 1 FROM jsonb_array_elements_text(c.value -> 'to') t WHERE lower(t) = 'blocked') ELSE false END AS after
            FROM revision r
            JOIN work_item w ON w.id = r.work_item_id
            CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c(value)
            WHERE r.work_item_id = ANY(@taskIds) AND c.value ->> 'field' = @tagsField
        ) changes
        WHERE before <> after
        ORDER BY at, rev
        """;

    // Live links of the three kinds that join two items of the tree.
    public const string Links =
        """
        SELECT s.key AS sourceKey, t.key AS targetKey, l.link_type AS kind
        FROM work_item_link l
        JOIN work_item s ON s.id = l.source_id
        JOIN work_item t ON t.id = l.target_id
        WHERE l.source_id = ANY(@ids) AND l.target_id = ANY(@ids) AND l.archived_at IS NULL
          AND l.link_type IN ('predecessor', 'successor', 'blockedBy')
        ORDER BY l.created_at, l.id
        """;

    public const string PlanApplied = "SELECT max(applied_at) FROM mcp_capture_plan_apply WHERE feature_id = @featureId";

    public const string Reviews =
        """
        SELECT w.key AS itemKey, e.at, e.verdict, e.note, e.by_user_id AS byUserId, e.agent_name AS agentName
        FROM review_event e
        JOIN work_item w ON w.id = e.work_item_id
        WHERE e.work_item_id = ANY(@taskIds)
        ORDER BY e.at, e.id
        """;

    // Day boundaries arrive as 'YYYY-MM-DD' text and are cast here, so no session zone is involved.
    public const string Sprints =
        """
        SELECT name, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate
        FROM sprint
        WHERE project_id = @projectId AND archived_at IS NULL
          AND start_date <= CAST(@toDay AS date) AND end_date >= CAST(@fromDay AS date)
        ORDER BY start_date, name
        """;

    public const string People = "SELECT id, name FROM app_user WHERE id = ANY(@userIds)";
}
