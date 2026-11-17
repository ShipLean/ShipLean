namespace ShipLean.Application.Features.InspectionMetrics.Facts.Sql;

// The SQL of the lead-time facts reader: one query per kind of fact. Instants are timestamptz; day bounds arrive as 'YYYY-MM-DD'
// text and are cast here to UTC midnights, so no session zone is involved. Only tasks are read (a BAU item is its own type and is
// never a task), and removed tasks are left out.
internal static class LeadTimeFactsSql
{
    // Every live commit of a task with its time: commit-titled url links (committed_at, null until stored) and the commits of a readable
    // repository (code_commit.authored_at, null until the commit has been read). Both carry the 7-character sha.
    private const string CommitRows =
        """
        SELECT l.source_id AS work_item_id, substring(l.title FROM char_length(@commitPrefix) + 1 FOR 7) AS sha7, l.committed_at AS committed_at,
               l.created_at AS created_at
        FROM work_item_link l
        WHERE l.link_type = 'url' AND l.archived_at IS NULL AND starts_with(l.title, @commitPrefix)
        UNION ALL
        SELECT c.work_item_id, left(c.sha, 7), cc.authored_at, c.created_at
        FROM work_item_commit c
        LEFT JOIN code_commit cc ON cc.repository_id = c.repository_id AND cc.sha = c.sha
        WHERE c.archived_at IS NULL
        """;

    // The tasks that could end in [fromDay, toDay + 2 days] (a first move to a Resolved-category state, or a commit time, inside that
    // span), and every live task of each Feature one of them belongs to, whatever its dates, so the Features tab can count a Feature's
    // tasks and its first start. AgentName is the agent of the task's first In-progress move.
    public const string Tasks =
        $$"""
        WITH live AS (
            SELECT t.id, t.key, t.title, t.assigned_to, t.state_id, p.key AS parent_key, p.type AS parent_type, f.id AS feature_id,
                   f.key AS feature_key, f.title AS feature_title
            FROM work_item t
            JOIN state s ON s.id = t.state_id
            LEFT JOIN work_item p ON p.id = t.parent_id AND p.type IN ('story', 'bug')
            LEFT JOIN work_item f ON f.id = p.parent_id AND f.type = 'feature'
            WHERE t.project_id = @projectId AND t.type = 'task' AND s.category <> @removed),
        windowed AS (
            SELECT l.id, l.feature_id
            FROM live l
            WHERE EXISTS (
                      SELECT 1
                      FROM (
                          SELECT min(m.changed_at) AS at
                          FROM revision m
                          CROSS JOIN LATERAL jsonb_array_elements(m.changes) AS c(value)
                          JOIN state ms ON ms.id::text = (c.value -> 'to') #>> '{}'
                          WHERE m.work_item_id = l.id AND c.value ->> 'field' = @stateField AND ms.category = @resolved) first_resolved
                      WHERE first_resolved.at >= (CAST(@fromDay AS timestamp) - interval '1 day') AT TIME ZONE 'UTC' AND first_resolved.at < (CAST(@toDay AS timestamp) + interval '3 days') AT TIME ZONE 'UTC')
                  OR EXISTS (
                      SELECT 1 FROM ({{CommitRows}}) k
                      WHERE k.work_item_id = l.id
                        AND k.committed_at >= (CAST(@fromDay AS timestamp) - interval '1 day') AT TIME ZONE 'UTC' AND k.committed_at < (CAST(@toDay AS timestamp) + interval '3 days') AT TIME ZONE 'UTC'))
        SELECT l.id, l.key, l.title, l.parent_key AS parentKey, l.parent_type AS parentType, l.feature_key AS featureKey, l.feature_title AS featureTitle,
               l.assigned_to AS assigneeId, (
                   SELECT m.agent_name
                   FROM revision m
                   CROSS JOIN LATERAL jsonb_array_elements(m.changes) AS c(value)
                   JOIN state ms ON ms.id::text = (c.value -> 'to') #>> '{}'
                   WHERE m.work_item_id = l.id AND c.value ->> 'field' = @stateField AND ms.category = @inProgress
                   ORDER BY m.changed_at, m.rev
                   LIMIT 1) AS agentName,
               s.name AS stateName, s.category AS stateCategory
        FROM live l
        JOIN state s ON s.id = l.state_id
        WHERE l.id IN (SELECT id FROM windowed) OR l.feature_id IN (SELECT feature_id FROM windowed WHERE feature_id IS NOT NULL)
        ORDER BY l.key
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

    // One row per sha7 of a task, preferring a row with a time.
    public const string Commits =
        $$"""
        SELECT w.key AS itemKey, k.sha7, k.committed_at AS committedAt
        FROM (
            SELECT DISTINCT ON (work_item_id, sha7) work_item_id, sha7, committed_at, created_at
            FROM ({{CommitRows}}) rows
            WHERE work_item_id = ANY(@taskIds)
            ORDER BY work_item_id, sha7, committed_at NULLS LAST
        ) k
        JOIN work_item w ON w.id = k.work_item_id
        ORDER BY k.created_at, w.key, k.sha7
        """;

    public const string People = "SELECT id, name FROM app_user WHERE id = ANY(@userIds)";
}
