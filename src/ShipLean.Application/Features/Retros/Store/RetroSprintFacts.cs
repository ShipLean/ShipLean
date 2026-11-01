using Dapper;
using Npgsql;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Retros.Store;

// Sprint facts read from revision history, for the retro views and the coverage rule. This is MetricsSql's logic for story-level
// items (story, bug and action), copied because MetricsSql is internal to Sprints: a sprint or state change records the new
// value in "to", and day boundaries are 00:00 UTC. If MetricsSql's completion rule changes, change this with it.
public static class RetroSprintFacts
{
    // Needs @itemIds, @sprintField and @stateField.
    private const string CompletionSql = """
        WITH ev AS (
            SELECT r.work_item_id AS item_id, r.rev, r.changed_at, c.value ->> 'field' AS field, (c.value -> 'to') #>> '{}' AS val
            FROM revision r
            CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c(value)
            WHERE r.work_item_id = ANY(@itemIds) AND c.value ->> 'field' IN (@sprintField, @stateField)
        ),
        mv AS (SELECT item_id, rev, changed_at, val::uuid AS sprint_id FROM ev WHERE field = @sprintField),
        st AS (
            SELECT ev.item_id, ev.rev, ev.changed_at, s.category
            FROM ev JOIN state s ON s.id = ev.val::uuid
            WHERE ev.field = @stateField
        ),
        latest AS (
            SELECT DISTINCT ON (item_id) item_id, rev, changed_at, category FROM st ORDER BY item_id, changed_at DESC, rev DESC
        )
        SELECT l.item_id AS itemId, l.changed_at AS completedAt,
               (SELECT m.sprint_id FROM mv m WHERE m.item_id = l.item_id AND (m.changed_at, m.rev) <= (l.changed_at, l.rev)
                 ORDER BY m.changed_at DESC, m.rev DESC LIMIT 1) AS sprintId
        FROM latest l
        WHERE l.category = 'completed'
        """;

    // Needs @sprintId, @storyLevel, @sprintField and @stateField. The completed rule is MetricsSql.CompletedFor.
    private const string SprintItemsSql = """
        WITH ev AS (
            SELECT r.work_item_id AS item_id, r.rev, r.changed_at, c.value ->> 'field' AS field, (c.value -> 'to') #>> '{}' AS val
            FROM revision r
            JOIN sprint sp0 ON sp0.id = @sprintId
            JOIN work_item w ON w.id = r.work_item_id AND w.type = ANY(@storyLevel) AND w.project_id = sp0.project_id
            CROSS JOIN LATERAL jsonb_array_elements(r.changes) AS c(value)
            WHERE c.value ->> 'field' IN (@sprintField, @stateField)
        ),
        mv AS (SELECT item_id, rev, changed_at, val::uuid AS sprint_id FROM ev WHERE field = @sprintField),
        st AS (
            SELECT ev.item_id, ev.rev, ev.changed_at, s.category
            FROM ev JOIN state s ON s.id = ev.val::uuid
            WHERE ev.field = @stateField
        )
        SELECT id AS itemId, completed AS finished, (carried AND NOT completed) AS rolledOver FROM (
            SELECT w.id,
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
                         AND coalesce((SELECT s.category FROM st s
                                        WHERE s.item_id = w.id
                                          AND CASE WHEN l.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC')
                                                   THEN (s.changed_at, s.rev) <= (l.changed_at, l.rev)
                                                   ELSE s.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC') END
                                        ORDER BY s.changed_at DESC, s.rev DESC LIMIT 1), '') = 'completed') AS completed,
                   ((coalesce(at_last.sprint_id = sp.id, false) OR coalesce(at_end.sprint_id = sp.id, false))
                    AND coalesce(end_state.category, '') NOT IN ('completed', 'removed')) AS carried
            FROM sprint sp
            JOIN project proj ON proj.id = sp.project_id
            JOIN work_item w ON w.project_id = sp.project_id AND w.type = ANY(@storyLevel)
            LEFT JOIN LATERAL (
                SELECT coalesce(max(g.day::date), sp.end_date) AS day
                FROM generate_series((sp.end_date - 6)::timestamp, sp.end_date::timestamp, interval '1 day') AS g(day)
                WHERE extract(isodow FROM g.day)::smallint = ANY (proj.working_days)
            ) last_work ON true
            LEFT JOIN LATERAL (SELECT m.sprint_id FROM mv m WHERE m.item_id = w.id AND m.changed_at <= (last_work.day::timestamp AT TIME ZONE 'UTC')
                               ORDER BY m.changed_at DESC, m.rev DESC LIMIT 1) at_last ON true
            LEFT JOIN LATERAL (SELECT m.sprint_id FROM mv m WHERE m.item_id = w.id AND m.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC')
                               ORDER BY m.changed_at DESC, m.rev DESC LIMIT 1) at_end ON true
            LEFT JOIN LATERAL (SELECT s.category FROM st s WHERE s.item_id = w.id AND s.changed_at < ((sp.end_date + 1)::timestamp AT TIME ZONE 'UTC')
                               ORDER BY s.changed_at DESC, s.rev DESC LIMIT 1) end_state ON true
            WHERE sp.id = @sprintId) outcome
        WHERE completed OR carried
        ORDER BY id
        """;

    // The items that are completed now, each with when it completed (its latest state change) and the sprint it sat in then.
    // An item that is not in a completed state is left out.
    public static async Task<IReadOnlyList<RetroItemCompletion>> CompletionsAsync(NpgsqlConnection connection, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<CompletionRow>(
            new CommandDefinition(CompletionSql, new { itemIds = itemIds.ToArray(), sprintField = FieldIds.Sprint, stateField = FieldIds.State }, cancellationToken: cancellationToken));
        return [.. rows.Select(row => new RetroItemCompletion(row.ItemId, new DateTimeOffset(DateTime.SpecifyKind(row.CompletedAt, DateTimeKind.Utc)), row.SprintId))];
    }

    // The story-level items that finished in the sprint, or were still open and in it at its end (rolled over).
    public static async Task<IReadOnlyList<RetroSprintItem>> SprintItemsAsync(NpgsqlConnection connection, Guid sprintId, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<RetroSprintItem>(
            new CommandDefinition(
                SprintItemsSql,
                new { sprintId, storyLevel = WorkItemTypeRules.StoryLevelDb, sprintField = FieldIds.Sprint, stateField = FieldIds.State },
                cancellationToken: cancellationToken))];

    private sealed record CompletionRow(Guid ItemId, DateTime CompletedAt, Guid? SprintId);
}
