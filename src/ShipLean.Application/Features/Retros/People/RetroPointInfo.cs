using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// A live point with the facts of its retro the writes need.
internal sealed record RetroPointInfo(Guid Id, Guid RetroId, Guid ProjectId, string Category, string Text, Guid? ThemeId, string Role, Guid? SprintId, Guid? WorkItemId)
{
    public RetroChanged Changed => new(ProjectId, SprintId is { } sprint ? [sprint] : [], WorkItemId is { } item ? [item] : []);

    private const string Select =
        """
        SELECT p.id, p.retrospective_id AS retroId, r.project_id AS projectId, p.category, p.text, p.theme_id AS themeId,
               r.role, r.sprint_id AS sprintId, r.work_item_id AS workItemId
        FROM retro_point p JOIN retrospective r ON r.id = p.retrospective_id
        WHERE p.id = @pointId AND p.archived_at IS NULL
        """;

    private const string SelectLocked = Select + "\nFOR UPDATE OF p";

    public static async Task<RetroPointInfo> LoadAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid pointId, CancellationToken cancellationToken, bool lockPoint = true)
        => await connection.QuerySingleOrDefaultAsync<RetroPointInfo>(
            new CommandDefinition(lockPoint ? SelectLocked : Select, new { pointId }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No retro point with that id was found.");
}
