using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Retros.People;

// What the people's writes share about themes: finding a live one in the project, and keeping its count and tone right
// after a point comes, goes or moves. Tone follows RetroPointRows: good when more than half the live points went well.
internal static class RetroThemeRows
{
    public static async Task RequireLiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid themeId, CancellationToken cancellationToken)
    {
        var found = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM retro_theme WHERE id = @themeId AND project_id = @projectId AND archived_at IS NULL AND merged_into_id IS NULL)",
                new { themeId, projectId },
                transaction,
                cancellationToken: cancellationToken));
        if (!found)
        {
            throw ApiException.NotFound("No theme with that id was found.");
        }
    }

    public static Task AdjustAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid themeId, int delta, CancellationToken cancellationToken)
        => connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE retro_theme t SET
                    point_count = greatest(t.point_count + @delta, 0),
                    tone = (SELECT CASE WHEN count(*) FILTER (WHERE p.category = 'wentWell') * 2 > count(*) THEN 'good' ELSE 'bad' END
                            FROM retro_point p WHERE p.theme_id = t.id AND p.archived_at IS NULL)
                WHERE t.id = @themeId
                """,
                new { themeId, delta },
                transaction,
                cancellationToken: cancellationToken));
}
