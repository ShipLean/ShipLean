using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Reads;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Decide;

namespace ShipLean.Application.Features.Retros.Decide;

// What the decide planner reads: the scope's points with their decision (K2), the open and the live action items, the themes and the merge pairs.
internal sealed class DecideScopeReader(NpgsqlDataSource dataSource)
{
    // The decision rule is the shared SQL fragment, so the text is built once.
    private static readonly string PointsSql = $"""
        SELECT p.id, p.text, p.category, t.name AS themeName, COALESCE(p.theme_set_by = 'person', false) AS themeByPerson, {RetroDecisionSql.Decision("p")} AS decision
        FROM retro_point p
        JOIN retrospective r ON r.id = p.retrospective_id
        LEFT JOIN work_item w ON w.id = r.work_item_id
        LEFT JOIN retro_theme t ON t.id = p.theme_id
        WHERE r.project_id = @projectId AND r.archived_at IS NULL AND p.archived_at IS NULL
          AND (@sprintId::uuid IS NULL OR r.sprint_id = @sprintId)
          AND (@featureId::uuid IS NULL OR r.work_item_id = @featureId OR w.parent_id = @featureId)
          AND (@since::timestamptz IS NULL OR p.created_at >= @since)
          AND (@until::timestamptz IS NULL OR p.created_at < @until)
        ORDER BY p.created_at, p.position, p.id
        """;

    // Every point of the scope that is not archived, wentWell ones too (they are told apart by their null decision). A date range is UTC days, both ends included.
    public async Task<IReadOnlyDictionary<Guid, DecideScopePoint>> PointsAsync(Guid projectId, DecideScope scope, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<PointRow>(
            new CommandDefinition(
                PointsSql,
                new
                {
                    projectId,
                    sprintId = scope.SprintId,
                    featureId = scope.FeatureId,
                    since = scope.From is { } from ? Utc(from) : (DateTime?)null,
                    until = scope.To is { } to ? Utc(to.AddDays(1)) : (DateTime?)null,
                },
                cancellationToken: cancellationToken));
        return rows.Select(Point).ToDictionary(point => point.Id);
    }

    // Open: the state's category is not completed or removed. These are what a new title is compared with.
    public async Task<IReadOnlyList<DecideOpenAction>> OpenActionsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<DecideOpenAction>(
            new CommandDefinition(
                """
                SELECT w.id, w.key, w.title, st.name AS state
                FROM retro_action ra JOIN work_item w ON w.id = ra.work_item_id JOIN state st ON st.id = w.state_id
                WHERE ra.project_id = @projectId AND st.category NOT IN ('completed', 'removed')
                ORDER BY w.created_at
                """,
                new { projectId },
                cancellationToken: cancellationToken))];
    }

    // Live, as AddPointsAsync means it: a retro action of the project whose state is not Removed.
    public async Task<DecideOpenAction?> LiveActionAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<DecideOpenAction>(
            new CommandDefinition(
                """
                SELECT w.id, w.key, w.title, st.name AS state
                FROM retro_action ra JOIN work_item w ON w.id = ra.work_item_id JOIN state st ON st.id = w.state_id
                WHERE ra.project_id = @projectId AND ra.work_item_id = @workItemId AND st.category <> 'removed'
                """,
                new { projectId, workItemId },
                cancellationToken: cancellationToken));
    }

    // The theme ids by name, as ProposeMergeAsync finds them; a name that finds no theme is missing from the result.
    public async Task<IReadOnlyDictionary<string, Guid>> ThemesAsync(Guid projectId, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string Name, Guid Id)>(
            new CommandDefinition(
                """
                SELECT DISTINCT ON (lower(name)) lower(name) AS name, id FROM retro_theme
                WHERE project_id = @projectId AND lower(name) = ANY(@names) AND archived_at IS NULL AND merged_into_id IS NULL
                ORDER BY lower(name), created_at
                """,
                new { projectId, names = names.Select(name => name.Trim().ToLowerInvariant()).ToArray() },
                cancellationToken: cancellationToken));
        return rows.ToDictionary(row => row.Name, row => row.Id);
    }

    // A pair with any row, pending, merged or kept apart, either way round.
    public async Task<bool> MergePairExistsAsync(Guid fromTheme, Guid intoTheme, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT EXISTS (SELECT 1 FROM retro_theme_merge_proposal WHERE archived_at IS NULL
                    AND LEAST(from_theme_id, into_theme_id) = LEAST(@fromTheme, @intoTheme) AND GREATEST(from_theme_id, into_theme_id) = GREATEST(@fromTheme, @intoTheme))
                """,
                new { fromTheme, intoTheme },
                cancellationToken: cancellationToken));
    }

    private static DateTime Utc(DateOnly day) => new(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Utc);

    private static DecideScopePoint Point(PointRow row)
        => new(
            row.Id,
            row.Text,
            Enum.Parse<RetroCategory>(row.Category, ignoreCase: true),
            row.Decision is null ? null : Enum.Parse<RetroDecision>(row.Decision, ignoreCase: true),
            row.ThemeName,
            row.ThemeByPerson);

    private sealed record PointRow(Guid Id, string Text, string Category, string? ThemeName, bool ThemeByPerson, string? Decision);
}
