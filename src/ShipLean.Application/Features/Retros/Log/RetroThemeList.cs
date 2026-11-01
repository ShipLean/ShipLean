using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Log;

// The themes a project shows: live, not merged away, and with at least one point.
internal static class RetroThemeList
{
    public static async Task<IReadOnlyList<RetroThemeRefDto>> LoadAsync(NpgsqlConnection connection, Guid projectId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<ThemeRow>(
            new CommandDefinition(
                """
                SELECT id, name, tone FROM retro_theme
                WHERE project_id = @projectId AND archived_at IS NULL AND merged_into_id IS NULL AND point_count > 0
                ORDER BY lower(name), id
                """,
                new { projectId },
                cancellationToken: cancellationToken));
        return [.. rows.Select(row => new RetroThemeRefDto(row.Id, row.Name, Enum.Parse<RetroTone>(row.Tone, ignoreCase: true)))];
    }

    private sealed record ThemeRow(Guid Id, string Name, string Tone);
}
