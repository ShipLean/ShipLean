using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Store;

// The one place a point is inserted. It adds the point, counts it on its theme and recomputes the theme's tone. Both write
// paths call it, one point at a time and after the themer has run, so the theme's centroid weights are right.
public static class RetroPointRows
{
    public static async Task<Guid> InsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid retroId,
        RetroCategory category,
        string text,
        string? rootCause,
        RetroThemeResult theme,
        Guid authorUserId,
        string? agentName,
        int position,
        CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO retro_point (id, retrospective_id, category, text, root_cause, theme_id, theme_set_by, embedding, created_by_user_id, agent_name, position)
                VALUES (@id, @retroId, @category, @text, @rootCause, @themeId, @setBy, @embedding, @authorUserId, @agentName, @position)
                """,
                new
                {
                    id,
                    retroId,
                    category = JsonNamingPolicy.CamelCase.ConvertName(category.ToString()),
                    text,
                    rootCause,
                    themeId = theme.ThemeId,
                    setBy = theme.SetBy is { } setBy ? JsonNamingPolicy.CamelCase.ConvertName(setBy.ToString()) : null,
                    embedding = theme.Embedding,
                    authorUserId,
                    agentName,
                    position,
                },
                transaction,
                cancellationToken: cancellationToken));
        if (theme.ThemeId is { } themeId)
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE retro_theme t SET
                        point_count = t.point_count + 1,
                        tone = (SELECT CASE WHEN count(*) FILTER (WHERE p.category = 'wentWell') * 2 > count(*) THEN 'good' ELSE 'bad' END
                                FROM retro_point p WHERE p.theme_id = t.id AND p.archived_at IS NULL)
                    WHERE t.id = @themeId
                    """,
                    new { themeId },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        return id;
    }
}
