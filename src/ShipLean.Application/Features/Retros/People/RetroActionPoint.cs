using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Retros.People;

// A live point as an action item write sees it: its project, its retro's sprint and item, and the state of its decision.
// LinkedItemId and LinkedState are the point's live join row (not archived) and the state category of that action item.
internal sealed record RetroActionPoint(Guid Id, Guid ProjectId, string Category, Guid? ThemeId, Guid? SprintId, Guid? WorkItemId, bool NotNeeded, Guid? LinkedItemId, string? LinkedState)
{
    private const string Select =
        """
        SELECT p.id, r.project_id AS projectId, p.category, p.theme_id AS themeId, r.sprint_id AS sprintId, r.work_item_id AS workItemId,
               (p.not_needed_reason IS NOT NULL) AS notNeeded, rap.work_item_id AS linkedItemId, st.category AS linkedState
        FROM retro_point p
        JOIN retrospective r ON r.id = p.retrospective_id
        LEFT JOIN retro_action_point rap ON rap.point_id = p.id AND rap.archived_at IS NULL
        LEFT JOIN work_item aw ON aw.id = rap.work_item_id
        LEFT JOIN state st ON st.id = aw.state_id
        WHERE p.id = ANY(@ids) AND p.archived_at IS NULL
        ORDER BY p.id
        """;

    // The named points in the order given. A point that is missing or belongs to another project is not_found. With lockRows the
    // points are locked in id order, so a Not needed or another action item's link waits for this write.
    public static async Task<IReadOnlyList<RetroActionPoint>> LoadAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, IReadOnlyList<Guid> pointIds, bool lockRows, CancellationToken cancellationToken)
    {
        var sql = lockRows ? Select + "\nFOR UPDATE OF p" : Select;
        var found = (await connection.QueryAsync<RetroActionPoint>(new CommandDefinition(sql, new { ids = pointIds.ToArray() }, transaction, cancellationToken: cancellationToken)))
            .Where(point => point.ProjectId == projectId)
            .ToDictionary(point => point.Id);
        if (pointIds.Any(id => !found.ContainsKey(id)))
        {
            throw ApiException.NotFound("No retro point with that id was found.");
        }

        return [.. pointIds.Select(id => found[id])];
    }

    // Went well is refused; a point that is Not needed or has a live action item is a conflict. Returns the Removed action item
    // each taken-over point had, by point id; the write archives that join row.
    public static IReadOnlyDictionary<Guid, Guid> RequireOpen(IReadOnlyList<RetroActionPoint> points)
    {
        if (points.Any(point => point.Category == "wentWell"))
        {
            throw ApiException.Validation("A Went well point needs no action item.", "pointIds");
        }

        if (points.Any(point => point.NotNeeded || (point.LinkedItemId is not null && point.LinkedState != "removed")))
        {
            throw ApiException.Conflict("This point is already decided.");
        }

        return points.Where(point => point.LinkedItemId is not null).ToDictionary(point => point.Id, point => point.LinkedItemId!.Value);
    }
}
