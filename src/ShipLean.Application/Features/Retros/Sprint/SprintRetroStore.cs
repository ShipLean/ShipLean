using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Store;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Retros.Sprint;

// The queries behind the sprint retro view. Retros are keyed by retrospective.sprint_id; only coverage reads the sprint's items.
internal static class SprintRetroStore
{
    public const int WindowSize = 6;

    public static async Task<SprintRow> SprintAsync(NpgsqlConnection connection, Guid sprintId, CancellationToken cancellationToken)
        => await connection.QuerySingleAsync<SprintRow>(
            new CommandDefinition(
                """
                SELECT id, project_id AS projectId, name, to_char(start_date, 'YYYY-MM-DD') AS start, to_char(end_date, 'YYYY-MM-DD') AS "end"
                FROM sprint WHERE id = @sprintId
                """,
                new { sprintId },
                cancellationToken: cancellationToken));

    // Every sprint of the project that starts no later than this one, oldest first, ending with this sprint.
    public static async Task<IReadOnlyList<SprintRow>> SeriesAsync(NpgsqlConnection connection, SprintRow sprint, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<SprintRow>(
            new CommandDefinition(
                """
                SELECT id, project_id AS projectId, name, to_char(start_date, 'YYYY-MM-DD') AS start, to_char(end_date, 'YYYY-MM-DD') AS "end"
                FROM sprint
                WHERE project_id = @projectId AND archived_at IS NULL AND (start_date, end_date, id) <= (@start::date, @end::date, @id)
                ORDER BY start_date, end_date, id
                """,
                new { projectId = sprint.ProjectId, start = sprint.Start, end = sprint.End, id = sprint.Id },
                cancellationToken: cancellationToken))];

    // The start of the next live sprint of the project after this one (same start_date, end_date, id order); null when there is none.
    public static async Task<string?> NextStartAsync(NpgsqlConnection connection, SprintRow sprint, CancellationToken cancellationToken)
        => await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                """
                SELECT to_char(start_date, 'YYYY-MM-DD')
                FROM sprint
                WHERE project_id = @projectId AND archived_at IS NULL AND (start_date, end_date, id) > (@start::date, @end::date, @id)
                ORDER BY start_date, end_date, id
                LIMIT 1
                """,
                new { projectId = sprint.ProjectId, start = sprint.Start, end = sprint.End, id = sprint.Id },
                cancellationToken: cancellationToken));

    public static async Task<IReadOnlyList<Guid>> RetroIdsAsync(NpgsqlConnection connection, Guid sprintId, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<Guid>(
            new CommandDefinition(
                "SELECT id FROM retrospective WHERE sprint_id = @sprintId AND archived_at IS NULL ORDER BY created_at, id",
                new { sprintId },
                cancellationToken: cancellationToken))];

    public static async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> PointDatesAsync(NpgsqlConnection connection, IReadOnlyList<Guid> retroIds, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<(Guid Id, DateTime CreatedAt)>(
            new CommandDefinition(
                "SELECT id, created_at AS createdAt FROM retro_point WHERE retrospective_id = ANY(@ids) AND archived_at IS NULL",
                new { ids = retroIds.ToArray() },
                cancellationToken: cancellationToken));
        return rows.ToDictionary(row => row.Id, row => new DateTimeOffset(DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc)));
    }

    public static async Task<IReadOnlyList<SprintMentionRow>> MentionsAsync(NpgsqlConnection connection, IReadOnlyList<Guid> sprintIds, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<SprintMentionRow>(
            new CommandDefinition(
                """
                SELECT p.theme_id AS themeId, r.sprint_id AS sprintId, p.category, th.name AS themeName, th.tone AS themeTone
                FROM retro_point p
                JOIN retrospective r ON r.id = p.retrospective_id
                JOIN retro_theme th ON th.id = p.theme_id
                WHERE r.sprint_id = ANY(@sprintIds) AND r.archived_at IS NULL AND p.archived_at IS NULL AND p.theme_id IS NOT NULL
                """,
                new { sprintIds = sprintIds.ToArray() },
                cancellationToken: cancellationToken))];

    // Action items raised in the sprint, and the open ones on the given themes.
    public static async Task<IReadOnlyList<SprintActionRow>> ActionsAsync(NpgsqlConnection connection, SprintRow sprint, IReadOnlyList<Guid> themeIds, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<SprintActionRow>(
            new CommandDefinition(
                $"""
                SELECT w.id AS itemId, w.key, w.title, w.story_points::numeric AS points, ra.theme_id AS themeId, th.name AS themeName, th.tone AS themeTone,
                       st.id AS stateId, st.name AS stateName, st.category AS stateCategory,
                       psp.id AS plannedId, psp.name AS plannedName, to_char(psp.start_date, 'YYYY-MM-DD') AS plannedStart, to_char(psp.end_date, 'YYYY-MM-DD') AS plannedEnd,
                       rsp.id AS raisedId, rsp.name AS raisedName, to_char(rsp.start_date, 'YYYY-MM-DD') AS raisedStart, to_char(rsp.end_date, 'YYYY-MM-DD') AS raisedEnd,
                       u.id AS ownerId, u.name AS ownerName, u.initials AS ownerInitials, u.avatar_colour AS ownerColour, {UserRefs.PhotoUrlSql} AS ownerPhotoUrl
                FROM retro_action ra
                JOIN work_item w ON w.id = ra.work_item_id
                JOIN state st ON st.id = w.state_id
                LEFT JOIN retro_theme th ON th.id = ra.theme_id
                LEFT JOIN sprint psp ON psp.id = w.sprint_id
                LEFT JOIN sprint rsp ON rsp.id = ra.raised_sprint_id
                LEFT JOIN app_user u ON u.id = w.assigned_to
                WHERE ra.project_id = @projectId AND st.category <> 'removed'
                  AND (ra.raised_sprint_id = @sprintId OR (ra.theme_id = ANY(@themeIds) AND st.category <> 'completed'))
                ORDER BY w.number
                """,
                new { projectId = sprint.ProjectId, sprintId = sprint.Id, themeIds = themeIds.ToArray() },
                cancellationToken: cancellationToken))];

    public static async Task<IReadOnlyList<SprintClosedActionRow>> ClosedActionsAsync(NpgsqlConnection connection, Guid projectId, IReadOnlyList<Guid> themeIds, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<SprintClosedActionRow>(
            new CommandDefinition(
                """
                SELECT ra.work_item_id AS itemId, ra.theme_id AS themeId
                FROM retro_action ra
                JOIN work_item w ON w.id = ra.work_item_id
                JOIN state st ON st.id = w.state_id
                WHERE ra.project_id = @projectId AND ra.theme_id = ANY(@themeIds) AND st.category = 'completed'
                """,
                new { projectId, themeIds = themeIds.ToArray() },
                cancellationToken: cancellationToken))];

    // The story-level items finished in the sprint, and the features with a story-level child that finished or rolled over from it
    // (taken from the sprint's own outcome, so a child that has since moved to a later sprint still counts), each with whether it has a live retro.
    public static async Task<IReadOnlyList<SprintCoverageRow>> CoverageAsync(NpgsqlConnection connection, Guid sprintId, CancellationToken cancellationToken)
    {
        var items = await RetroSprintFacts.SprintItemsAsync(connection, sprintId, cancellationToken);
        var finished = items.Where(item => item.Finished).Select(item => item.ItemId).ToArray();
        var children = items.Where(item => item.Finished || item.RolledOver).Select(item => item.ItemId).ToArray();
        return [.. await connection.QueryAsync<SprintCoverageRow>(
            new CommandDefinition(
                """
                SELECT w.id, w.key, w.number, w.type, w.title, false AS isFeature, true AS lastChildCompleted,
                       EXISTS (SELECT 1 FROM retrospective r WHERE r.work_item_id = w.id AND r.archived_at IS NULL) AS hasRetro
                FROM work_item w
                WHERE w.id = ANY(@finished)
                UNION ALL
                SELECT f.id, f.key, f.number, f.type, f.title, true,
                       NOT EXISTS (
                           SELECT 1 FROM work_item k JOIN state ks ON ks.id = k.state_id
                           WHERE k.parent_id = f.id AND k.type = ANY(@storyLevel) AND ks.category NOT IN ('completed', 'removed')),
                       EXISTS (SELECT 1 FROM retrospective r WHERE r.work_item_id = f.id AND r.archived_at IS NULL)
                FROM work_item f
                WHERE f.type = 'feature' AND EXISTS (
                    SELECT 1 FROM work_item c
                    WHERE c.parent_id = f.id AND c.id = ANY(@children))
                ORDER BY number
                """,
                new { finished, children, storyLevel = WorkItemTypeRules.StoryLevelDb },
                cancellationToken: cancellationToken))];
    }
}
