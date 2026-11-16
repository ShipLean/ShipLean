namespace ShipLean.Application.Features.InspectionMetrics.Facts.Sql;

// The SQL of the lead-time facts reader: one query per kind of fact. Instants are timestamptz; day bounds arrive as 'YYYY-MM-DD'
// text and are cast here to UTC midnights, so no session zone is involved. Only tasks are read (a BAU item is its own type and is
// never a task), and removed tasks are left out.
internal static class LeadTimeFactsSql
{
    // The tasks that could end in [fromDay, toDay + 2 days]: a first move to a Resolved-category state, or a commit time, inside
    // that span. AgentName is the agent of the task's first In-progress move.
    public const string Tasks =
        """
        SELECT t.id, t.key, t.title, p.key AS parentKey, p.type AS parentType, f.key AS featureKey, f.title AS featureTitle,
               t.assigned_to AS assigneeId, (
                   SELECT m.agent_name
                   FROM revision m
                   CROSS JOIN LATERAL jsonb_array_elements(m.changes) AS c(value)
                   JOIN state ms ON ms.id::text = (c.value -> 'to') #>> '{}'
                   WHERE m.work_item_id = t.id AND c.value ->> 'field' = @stateField AND ms.category = @inProgress
                   ORDER BY m.changed_at, m.rev
                   LIMIT 1) AS agentName,
               s.name AS stateName, s.category AS stateCategory
        FROM work_item t
        JOIN state s ON s.id = t.state_id
        LEFT JOIN work_item p ON p.id = t.parent_id AND p.type IN ('story', 'bug')
        LEFT JOIN work_item f ON f.id = p.parent_id AND f.type = 'feature'
        WHERE t.project_id = @projectId AND t.type = 'task' AND s.category <> @removed
          AND (EXISTS (
                   SELECT 1
                   FROM (
                       SELECT min(m.changed_at) AS at
                       FROM revision m
                       CROSS JOIN LATERAL jsonb_array_elements(m.changes) AS c(value)
                       JOIN state ms ON ms.id::text = (c.value -> 'to') #>> '{}'
                       WHERE m.work_item_id = t.id AND c.value ->> 'field' = @stateField AND ms.category = @resolved) first_resolved
                   WHERE first_resolved.at >= (CAST(@fromDay AS timestamp) - interval '1 day') AT TIME ZONE 'UTC' AND first_resolved.at < (CAST(@toDay AS timestamp) + interval '3 days') AT TIME ZONE 'UTC')
               OR EXISTS (
                   SELECT 1 FROM work_item_link l
                   WHERE l.source_id = t.id AND l.link_type = 'url' AND l.archived_at IS NULL AND starts_with(l.title, @commitPrefix)
                     AND l.committed_at >= (CAST(@fromDay AS timestamp) - interval '1 day') AT TIME ZONE 'UTC' AND l.committed_at < (CAST(@toDay AS timestamp) + interval '3 days') AT TIME ZONE 'UTC'))
        ORDER BY t.key
        """;

    // Every state move, revision 1 included, with the target state's name and category.
    public const string Moves =
        """
        SELECT w.key AS itemKey, r.changed_at AS at, r.rev, s.name AS stateName, s.category, r.agent_name AS agentName, r.changed_by AS userId
        FROM revision r
        JOIN work_item w ON w.id = r.work_item_id
        CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c(value)
        JOIN state s ON s.id::text = (c.value -> 'to') #>> '{}'
        WHERE r.work_item_id = ANY(@taskIds) AND c.value ->> 'field' = @stateField
        ORDER BY r.changed_at, r.rev
        """;

    // Every tags change after which the Blocked tag (any case) is in the set when it was not, or the reverse.
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

    public const string Reviews =
        """
        SELECT w.key AS itemKey, e.at, e.verdict
        FROM review_event e
        JOIN work_item w ON w.id = e.work_item_id
        WHERE e.work_item_id = ANY(@taskIds)
        ORDER BY e.at, e.id
        """;

    // Live commit links: the title is "commit <sha7> <subject>".
    public const string Commits =
        """
        SELECT w.key AS itemKey, substring(l.title FROM char_length(@commitPrefix) + 1 FOR 7) AS sha7, l.committed_at AS committedAt
        FROM work_item_link l
        JOIN work_item w ON w.id = l.source_id
        WHERE l.source_id = ANY(@taskIds) AND l.link_type = 'url' AND l.archived_at IS NULL AND starts_with(l.title, @commitPrefix)
        ORDER BY l.created_at, l.id
        """;

    public const string People = "SELECT id, name FROM app_user WHERE id = ANY(@userIds)";
}
