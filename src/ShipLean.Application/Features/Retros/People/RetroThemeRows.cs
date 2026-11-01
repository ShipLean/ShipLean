using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Retros.People;

// What the people's writes share about themes: finding a live one in the project, and keeping its count and tone right
// after a point comes, goes or moves. Tone follows RetroPointRows: good when more than half the live points went well.
internal static class RetroThemeRows
{
    // FOR NO KEY UPDATE, not FOR SHARE: the caller goes on to UPDATE this row (count and tone), and two writers that each held
    // a share lock would deadlock on that upgrade. This way concurrent adds to one theme queue, and a Merge's FOR UPDATE still waits.
    public static async Task RequireLiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid themeId, CancellationToken cancellationToken)
    {
        var found = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM (SELECT id FROM retro_theme WHERE id = @themeId AND project_id = @projectId AND archived_at IS NULL AND merged_into_id IS NULL FOR NO KEY UPDATE) live)",
                new { themeId, projectId },
                transaction,
                cancellationToken: cancellationToken));
        if (!found)
        {
            throw ApiException.NotFound("No theme with that id was found.");
        }
    }

    // Takes the row locks of the given themes in id order. Every write that changes more than one theme starts here, so two of
    // them (Move A to B against Move B to A, or a Move against a Merge) can't each hold one lock and wait for the other's.
    public static Task LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyCollection<Guid> themeIds, CancellationToken cancellationToken)
        => connection.ExecuteAsync(
            new CommandDefinition("SELECT id FROM retro_theme WHERE id = ANY(@themeIds) ORDER BY id FOR UPDATE", new { themeIds = themeIds.ToArray() }, transaction, cancellationToken: cancellationToken));

    // Adjusts the live themes among the given ones, in id order so two writes touching the same pair take the row locks in the
    // same order. A theme merged or archived since it was checked is skipped by the update; that is a 409 for a target the
    // caller needs (required) and nothing to do for one it is leaving.
    public static async Task AdjustAllAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, IEnumerable<(Guid ThemeId, int Delta, bool Required)> changes, CancellationToken cancellationToken)
    {
        foreach (var (themeId, delta, required) in changes.OrderBy(c => c.ThemeId))
        {
            var updated = await AdjustAsync(connection, transaction, themeId, delta, cancellationToken);
            if (updated == 0 && required)
            {
                throw ApiException.Conflict("That theme was merged or removed. Pick the theme again.");
            }
        }
    }

    public static Task<int> AdjustAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid themeId, int delta, CancellationToken cancellationToken)
        => connection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE retro_theme t SET
                    point_count = greatest(t.point_count + @delta, 0),
                    tone = (SELECT CASE WHEN count(*) FILTER (WHERE p.category = 'wentWell') * 2 > count(*) THEN 'good' ELSE 'bad' END
                            FROM retro_point p WHERE p.theme_id = t.id AND p.archived_at IS NULL)
                WHERE t.id = @themeId AND t.merged_into_id IS NULL AND t.archived_at IS NULL
                """,
                new { themeId, delta },
                transaction,
                cancellationToken: cancellationToken));
}
