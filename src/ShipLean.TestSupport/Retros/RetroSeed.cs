using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;

namespace ShipLean.TestSupport.Retros;

// Inserts retro rows straight into the test database, so a read lane arranges retros without the write path. Its callers
// create the work items, sprints and users with the existing arrangement helpers and pass their ids in. A theme's tone is
// what the caller says (RetroPointRows recomputes it on a real insert); a themed point is counted on its theme.
public sealed class RetroSeed(NpgsqlConnection connection, Guid projectId)
{
    public async Task<Guid> ThemeAsync(string name, RetroTone tone = RetroTone.Bad, RetroThemeSetBy nameSetBy = RetroThemeSetBy.Agent)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            "INSERT INTO retro_theme (id, project_id, name, name_set_by, tone) VALUES (@id, @projectId, @name, @setBy, @tone)",
            new { id, projectId, name, setBy = Text(nameSetBy), tone = Text(tone) });
        return id;
    }

    // A theme that was merged into another: it keeps its name so a pick by that name follows to the target.
    public async Task<Guid> MergedThemeAsync(string name, Guid intoThemeId)
    {
        var id = await ThemeAsync(name);
        await connection.ExecuteAsync("UPDATE retro_theme SET merged_into_id = @intoThemeId WHERE id = @id", new { id, intoThemeId });
        return id;
    }

    public async Task<Guid> RetroAsync(RetroSeedRetro retro)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO retrospective (id, project_id, work_item_id, sprint_id, author_user_id, agent_name, role, partial, created_at, updated_at)
            VALUES (@id, @projectId, @workItemId, @sprintId, @authorId, @agentName, @role, @partial::boolean, coalesce(@createdAt::timestamptz, now()), coalesce(@createdAt, now()))
            """,
            new { id, projectId, workItemId = retro.WorkItemId, sprintId = retro.SprintId, authorId = retro.AuthorId, agentName = retro.AgentName, role = Text(retro.Role), partial = retro.Partial, createdAt = retro.CreatedAt });
        return id;
    }

    public async Task<Guid> PointAsync(Guid retroId, RetroSeedPoint point)
    {
        var id = Guid.NewGuid();
        var retro = await connection.QuerySingleAsync<(Guid AuthorId, string? AgentName)>(
            "SELECT author_user_id AS AuthorId, agent_name AS AgentName FROM retrospective WHERE id = @retroId", new { retroId });
        var position = point.Position ?? await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM retro_point WHERE retrospective_id = @retroId", new { retroId });
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_point (id, retrospective_id, category, text, root_cause, theme_id, theme_set_by, created_by_user_id, agent_name, position, created_at, archived_at)
            VALUES (@id, @retroId, @category, @text, @rootCause, @themeId, @setBy, @authorId, @agentName, @position, coalesce(@createdAt, now()), CASE WHEN @archived THEN now() END)
            """,
            new
            {
                id,
                retroId,
                category = Text(point.Category),
                text = point.Text,
                rootCause = point.RootCause,
                themeId = point.ThemeId,
                setBy = point.ThemeId is null ? null : Text(point.ThemeSetBy ?? RetroThemeSetBy.Agent),
                authorId = point.AuthorId ?? retro.AuthorId,
                agentName = point.AgentName ?? retro.AgentName,
                position,
                createdAt = point.CreatedAt,
                archived = point.Archived,
            });
        if (point.ThemeId is { } themeId && !point.Archived)
        {
            await connection.ExecuteAsync("UPDATE retro_theme SET point_count = point_count + 1 WHERE id = @themeId", new { themeId });
        }

        return id;
    }

    public async Task FailureAsync(Guid retroId, RetroFailureKind kind, string text, int position = 0)
        => await connection.ExecuteAsync(
            "INSERT INTO retro_failure_log (id, retrospective_id, kind, text, position) VALUES (@id, @retroId, @kind, @text, @position)",
            new { id = Guid.NewGuid(), retroId, kind = Text(kind), text, position });

    // retroId was compiled from sourceRetroId.
    public async Task SourceAsync(Guid retroId, Guid sourceRetroId)
        => await connection.ExecuteAsync(
            "INSERT INTO retrospective_source (retrospective_id, source_retrospective_id) VALUES (@retroId, @sourceRetroId)",
            new { retroId, sourceRetroId });

    public async Task ActionAsync(RetroSeedAction action)
        => await connection.ExecuteAsync(
            "INSERT INTO retro_action (work_item_id, project_id, theme_id, point_id, raised_sprint_id) VALUES (@workItemId, @projectId, @themeId, @pointId, @raisedSprintId)",
            new { workItemId = action.WorkItemId, projectId, themeId = action.ThemeId, pointId = action.PointId, raisedSprintId = action.RaisedSprintId });

    private static string Text<T>(T value)
        where T : struct, Enum
        => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
