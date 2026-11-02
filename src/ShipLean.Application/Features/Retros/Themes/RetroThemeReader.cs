using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Themes;

// The themes the pickers offer: live, not merged away, and with at least one point, by name, with how many points each has.
public sealed class RetroThemeReader(NpgsqlDataSource dataSource, IRetroThemer themer) : IRetroThemeReader
{
    public async Task<RetroThemeListDto> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ThemeRow>(
            new CommandDefinition(
                """
                SELECT id, name, tone, point_count AS pointCount FROM retro_theme
                WHERE project_id = @projectId AND archived_at IS NULL AND merged_into_id IS NULL AND point_count > 0
                ORDER BY lower(name), id
                """,
                new { projectId },
                cancellationToken: cancellationToken));
        return new RetroThemeListDto(
            themer.Mode,
            [.. rows.Select(row => new RetroThemeOptionDto(new RetroThemeRefDto(row.Id, row.Name, Enum.Parse<RetroTone>(row.Tone, ignoreCase: true)), row.PointCount))]);
    }

    private sealed record ThemeRow(Guid Id, string Name, string Tone, int PointCount);
}
