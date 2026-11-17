namespace ShipLean.Application.Features.InspectionMetrics.Quality.Sql;

// The SQL of the quality facts reader (QK4), in the lead-time reader's style: day bounds arrive as 'YYYY-MM-DD' text and are cast
// here to UTC midnights, (fromDay - 1 day) 00:00Z up to (toDay + 3 days) 00:00Z, so no session zone is involved. Removed items are
// left out. The cause tasks' moves, tags, reviews, commits and people reuse LeadTimeFactsSql's per-task queries.
internal static class QualityFactsSql
{
    private const string From = "(CAST(@fromDay AS timestamp) - interval '1 day') AT TIME ZONE 'UTC'";
    private const string To = "(CAST(@toDay AS timestamp) + interval '3 days') AT TIME ZONE 'UTC'";

    // Whether the project has any live Bug, whatever its date.
    public const string AnyBugs =
        """
        SELECT EXISTS (
            SELECT 1
            FROM work_item b
            JOIN state s ON s.id = b.state_id
            WHERE b.project_id = @projectId AND b.type = 'bug' AND s.category <> @removed)
        """;

    // Every live Bug filed in the span, oldest first, with the live Task its first live Caused by link points at (null without one).
    public const string Bugs =
        $$"""
        SELECT b.key, b.title, b.severity::int AS severity, b.created_at AS filedAt, b.found_before_release AS foundBeforeRelease, cause.id AS causeId,
               cause.key AS causedByKey
        FROM work_item b
        JOIN state s ON s.id = b.state_id
        LEFT JOIN LATERAL (
            SELECT t.id, t.key
            FROM work_item_link l
            JOIN work_item t ON t.id = l.target_id AND t.type = 'task'
            JOIN state ts ON ts.id = t.state_id
            WHERE l.source_id = b.id AND l.link_type = @causedBy AND l.archived_at IS NULL AND ts.category <> @removed
            ORDER BY l.created_at, t.key
            LIMIT 1) cause ON true
        WHERE b.project_id = @projectId AND b.type = 'bug' AND s.category <> @removed
          AND b.created_at >= {{From}} AND b.created_at < {{To}}
        ORDER BY b.created_at, b.key
        """;

    // Every pull request linked (a live url link) to a live item of the project that merged in the span, once per url.
    public const string MergedPrs =
        $$"""
        SELECT ls.url, ls.merged_at AS mergedAt
        FROM link_state ls
        WHERE ls.merged_at >= {{From}} AND ls.merged_at < {{To}}
          AND EXISTS (
              SELECT 1
              FROM work_item_link l
              JOIN work_item w ON w.id = l.source_id
              JOIN state s ON s.id = w.state_id
              WHERE l.link_type = 'url' AND l.archived_at IS NULL AND link_url_key(l.url) = ls.url_key
                AND w.project_id = @projectId AND s.category <> @removed)
        ORDER BY ls.merged_at, ls.url
        """;

    public const string SeverityWeights = "SELECT bug_severity_weights::text FROM project WHERE id = @projectId";

    // The cause tasks by id, shaped as LeadTimeFactsSql.Tasks shapes a task; AgentName is the agent of the first In-progress move.
    public const string CauseTasks =
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
        WHERE t.id = ANY(@taskIds)
        ORDER BY t.key
        """;
}
