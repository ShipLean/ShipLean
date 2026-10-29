namespace ShipLean.Application.Features.Sprints.Metrics;

// The sprint and state history of a project's stories and bugs, unpacked from revision.changes. A sprint or state change
// records the new value in "to"; an item created in a sprint has its sprint change in revision 1, one created outside any
// sprint has none. Day boundaries are 00:00 UTC.
internal static class MetricsSql
{
    // Needs @projectId, @sprintField and @stateField (FieldIds.Sprint, FieldIds.State).
    internal const string History = """
        ev AS (
            SELECT r.work_item_id AS item_id, r.rev, r.changed_at, c.value ->> 'field' AS field, (c.value -> 'to') #>> '{}' AS val
            FROM revision r
            JOIN work_item w ON w.id = r.work_item_id AND w.type IN ('story', 'bug') AND w.project_id = @projectId
            CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c(value)
            WHERE c.value ->> 'field' IN (@sprintField, @stateField)
        ),
        mv AS (SELECT item_id, rev, changed_at, val::uuid AS sprint_id FROM ev WHERE field = @sprintField),
        st AS (
            SELECT ev.item_id, ev.rev, ev.changed_at, s.category
            FROM ev JOIN state s ON s.id = ev.val::uuid
            WHERE ev.field = @stateField
        )
        """;

    // True when item w completed in sprint sp: the rule Figures sums and the per-item outcomes report (needs History's st and mv).
    internal const string CompletedFor = """
        EXISTS (
                    SELECT 1
                    FROM st e
                    LEFT JOIN LATERAL (
                        SELECT m.changed_at, m.rev FROM mv m
                        WHERE m.item_id = w.id AND (m.changed_at, m.rev) > (e.changed_at, e.rev) AND m.sprint_id IS DISTINCT FROM sp.id
                        ORDER BY m.changed_at, m.rev LIMIT 1) l ON true
                    WHERE e.item_id = w.id AND e.category = 'completed'
                      AND e.changed_at >= (sp.start_date::timestamp AT TIME ZONE 'UTC')
                      AND e.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC')
                      AND (SELECT m.sprint_id FROM mv m
                            WHERE m.item_id = w.id AND (m.changed_at, m.rev) <= (e.changed_at, e.rev)
                            ORDER BY m.changed_at DESC, m.rev DESC LIMIT 1) = sp.id
                      -- still completed when the sprint ended, or when it left the sprint if that came first
                      AND coalesce((SELECT s.category FROM st s
                                     WHERE s.item_id = w.id
                                       AND CASE WHEN l.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC')
                                                THEN (s.changed_at, s.rev) <= (l.changed_at, l.rev)
                                                ELSE s.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC') END
                                     ORDER BY s.changed_at DESC, s.rev DESC LIMIT 1), '') = 'completed')
        """;

    // Committed and completed points of one sprint (@sprintId, @projectId).
    public const string Figures = $$"""
        WITH {{History}}
        SELECT
            (SELECT coalesce(sum(w.story_points), 0) FROM work_item w
              WHERE w.project_id = sp.project_id AND w.type IN ('story', 'bug')
                AND (SELECT m.sprint_id FROM mv m
                      WHERE m.item_id = w.id AND m.changed_at <= (sp.start_date::timestamp AT TIME ZONE 'UTC')
                      ORDER BY m.changed_at DESC, m.rev DESC LIMIT 1) = sp.id)::numeric AS committed,
            (SELECT coalesce(sum(w.story_points), 0) FROM work_item w
              WHERE w.project_id = sp.project_id AND w.type IN ('story', 'bug')
                AND {{CompletedFor}})::numeric AS completed
        FROM sprint sp
        WHERE sp.id = @sprintId
        """;

    // Every story and bug that was committed, added, completed or carried over in one sprint (@sprintId, @projectId). at_start,
    // at_last and at_end are the sprint the item sat in at 00:00 on the first day, at 00:00 on the last working day on or
    // before the end, and at the end of the last calendar day. A two-week sprint that starts on Monday ends on Sunday, and
    // the rollover happens on the Friday, so "carried" has to see that Friday. end_state is the category at the end of the
    // last calendar day.
    public const string Items = $$"""
        WITH {{History}}
        SELECT * FROM (
            SELECT w.id, w.key, w.title, w.type, w.story_points::numeric AS points,
                   coalesce(at_start.sprint_id = sp.id, false) AS committed,
                   (at_start.sprint_id IS DISTINCT FROM sp.id
                    AND (coalesce(at_last.sprint_id = sp.id, false) OR coalesce(at_end.sprint_id = sp.id, false))
                    AND coalesce(end_state.category, '') <> 'removed'
                    AND EXISTS (SELECT 1 FROM mv m WHERE m.item_id = w.id AND m.sprint_id = sp.id
                                 AND m.changed_at > (sp.start_date::timestamp AT TIME ZONE 'UTC')
                                 AND m.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC'))) AS added,
                   {{CompletedFor}} AS completed,
                   ((coalesce(at_last.sprint_id = sp.id, false) OR coalesce(at_end.sprint_id = sp.id, false))
                    AND coalesce(end_state.category, '') NOT IN ('completed', 'removed')
                    AND NOT {{CompletedFor}}) AS carried
            FROM sprint sp
            JOIN project proj ON proj.id = sp.project_id
            JOIN work_item w ON w.project_id = sp.project_id AND w.type IN ('story', 'bug')
            LEFT JOIN LATERAL (
                SELECT coalesce(max(g.day::date), sp.end_date) AS day
                FROM generate_series((sp.end_date - 6)::timestamp, sp.end_date::timestamp, interval '1 day') AS g(day)
                WHERE extract(isodow FROM g.day)::smallint = ANY (proj.working_days)
            ) last_work ON true
            LEFT JOIN LATERAL (SELECT m.sprint_id FROM mv m WHERE m.item_id = w.id AND m.changed_at <= (sp.start_date::timestamp AT TIME ZONE 'UTC')
                               ORDER BY m.changed_at DESC, m.rev DESC LIMIT 1) at_start ON true
            LEFT JOIN LATERAL (SELECT m.sprint_id FROM mv m WHERE m.item_id = w.id AND m.changed_at <= (last_work.day::timestamp AT TIME ZONE 'UTC')
                               ORDER BY m.changed_at DESC, m.rev DESC LIMIT 1) at_last ON true
            LEFT JOIN LATERAL (SELECT m.sprint_id FROM mv m WHERE m.item_id = w.id AND m.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC')
                               ORDER BY m.changed_at DESC, m.rev DESC LIMIT 1) at_end ON true
            LEFT JOIN LATERAL (SELECT s.category FROM st s WHERE s.item_id = w.id AND s.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC')
                               ORDER BY s.changed_at DESC, s.rev DESC LIMIT 1) end_state ON true
            WHERE sp.id = @sprintId) outcome
        WHERE committed OR added OR completed OR carried
        ORDER BY key COLLATE "C"
        """;

    // Remaining points at the end of each calendar day from the sprint's first day to @last (@sprintId, @projectId, @last).
    public const string Remaining = $$"""
        WITH {{History}}
        SELECT to_char(d.day, 'YYYY-MM-DD') AS day,
               (SELECT coalesce(sum(w.story_points), 0) FROM work_item w
                 WHERE w.project_id = sp.project_id AND w.type IN ('story', 'bug')
                   AND (SELECT m.sprint_id FROM mv m
                         WHERE m.item_id = w.id AND m.changed_at < ((d.day + 1)::timestamp AT TIME ZONE 'UTC')
                         ORDER BY m.changed_at DESC, m.rev DESC LIMIT 1) = sp.id
                   AND coalesce((SELECT s.category FROM st s
                                  WHERE s.item_id = w.id AND s.changed_at < ((d.day + 1)::timestamp AT TIME ZONE 'UTC')
                                  ORDER BY s.changed_at DESC, s.rev DESC LIMIT 1), '') NOT IN ('completed', 'removed'))::numeric AS remaining
        FROM sprint sp
        CROSS JOIN LATERAL (SELECT g::date AS day FROM generate_series(sp.start_date::timestamp, @last::timestamp, interval '1 day') AS g) d
        WHERE sp.id = @sprintId
        ORDER BY d.day
        """;

    public const string SelectSprint =
        """
        SELECT sp.id, sp.project_id AS projectId, sp.name, to_char(sp.start_date, 'YYYY-MM-DD') AS startDate,
               to_char(sp.end_date, 'YYYY-MM-DD') AS endDate
        FROM sprint sp WHERE sp.id = @sprintId AND sp.archived_at IS NULL
        """;

    public const string SelectProject =
        "SELECT velocity_window AS velocityWindow, velocity_override AS velocityOverride, array_to_string(working_days, ',') AS workingDays FROM project WHERE id = @projectId";

    public const string EndedSprints =
        """
        SELECT * FROM (
            SELECT sp.id, sp.project_id AS projectId, sp.name, to_char(sp.start_date, 'YYYY-MM-DD') AS startDate,
                   to_char(sp.end_date, 'YYYY-MM-DD') AS endDate
            FROM sprint sp
            WHERE sp.project_id = @projectId AND sp.archived_at IS NULL AND sp.end_date < @today
            ORDER BY sp.start_date DESC LIMIT @window) latest
        ORDER BY latest.startDate
        """;

    // The project's sprints in date order with the team's entered capacity (null when nobody entered any) and the points
    // of the stories and bugs sitting in each now.
    public const string ForecastSprints =
        """
        SELECT sp.id, sp.name, to_char(sp.start_date, 'YYYY-MM-DD') AS startDate,
               (SELECT sum(c.capacity_points) FROM sprint_capacity c WHERE c.sprint_id = sp.id AND c.archived_at IS NULL)::numeric AS capacity,
               (SELECT coalesce(sum(w.story_points), 0) FROM work_item w
                 WHERE w.sprint_id = sp.id AND w.type IN ('story', 'bug'))::numeric AS planned
        FROM sprint sp
        WHERE sp.project_id = @projectId AND sp.archived_at IS NULL
        ORDER BY sp.start_date, sp.name
        """;

    // The unplanned backlog in backlog order.
    public const string Backlog =
        """
        SELECT w.id, w.story_points::numeric AS points
        FROM work_item w
        JOIN state s ON s.id = w.state_id
        WHERE w.project_id = @projectId AND w.sprint_id IS NULL AND w.type IN ('story', 'bug')
          AND s.category NOT IN ('completed', 'removed')
        ORDER BY w.backlog_order COLLATE "C", w.number
        """;
}
