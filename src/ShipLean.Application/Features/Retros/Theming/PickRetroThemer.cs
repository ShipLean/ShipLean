using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Theming;

// The theming seam's pick mode: the agent or person names the theme and this resolves it. A name matches a live theme
// case-insensitively and follows merged_into_id to the theme that absorbed it; an unknown name creates a theme; no name leaves
// the point unthemed. The theme's tone is not decided here, RetroPointRows recomputes it on every insert.
public sealed class PickRetroThemer(NpgsqlDataSource dataSource) : IRetroThemer
{
    private const int MaxMergeHops = 20;

    public RetroThemeMode Mode => RetroThemeMode.Pick;

    public async Task<RetroThemeResult> AssignAsync(RetroThemeRequest request, CancellationToken cancellationToken)
    {
        var name = request.SuppliedTheme?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return new RetroThemeResult(null, null, null, null);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var themeId = await FindAsync(connection, request.ProjectId, name, cancellationToken)
            ?? await CreateAsync(connection, request, name, cancellationToken)
            ?? await FindAsync(connection, request.ProjectId, name, cancellationToken);
        return new RetroThemeResult(themeId, request.IsPerson ? RetroThemeSetBy.Person : RetroThemeSetBy.Agent, null, null);
    }

    private static async Task<Guid?> FindAsync(NpgsqlConnection connection, Guid projectId, string name, CancellationToken cancellationToken)
    {
        var current = await connection.QueryFirstOrDefaultAsync<ThemeRow>(
            new CommandDefinition(
                """
                SELECT id, merged_into_id AS mergedIntoId FROM retro_theme
                WHERE project_id = @projectId AND lower(name) = lower(@name) AND (archived_at IS NULL OR merged_into_id IS NOT NULL)
                ORDER BY (merged_into_id IS NOT NULL), created_at
                LIMIT 1
                """,
                new { projectId, name },
                cancellationToken: cancellationToken));
        for (var hop = 0; current?.MergedIntoId is { } next && hop < MaxMergeHops; hop++)
        {
            current = await connection.QuerySingleAsync<ThemeRow>(
                new CommandDefinition("SELECT id, merged_into_id AS mergedIntoId FROM retro_theme WHERE id = @next", new { next }, cancellationToken: cancellationToken));
        }

        return current?.Id;
    }

    // Null when another request created the same name first; the caller looks it up again.
    private static Task<Guid?> CreateAsync(NpgsqlConnection connection, RetroThemeRequest request, string name, CancellationToken cancellationToken)
        => connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                """
                INSERT INTO retro_theme (id, project_id, name, name_set_by, tone)
                VALUES (@id, @projectId, @name, @setBy, 'bad')
                ON CONFLICT (project_id, lower(name)) WHERE archived_at IS NULL AND merged_into_id IS NULL DO NOTHING
                RETURNING id
                """,
                new { id = Guid.NewGuid(), projectId = request.ProjectId, name, setBy = request.IsPerson ? "person" : "agent" },
                cancellationToken: cancellationToken));

    private sealed record ThemeRow(Guid Id, Guid? MergedIntoId);
}
