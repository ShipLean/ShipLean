using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Reads;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.Core;

namespace ShipLean.Application.Features.Retros.ActionDialog;

// What the action dialog offers (contract K6): the selected points, the project's undecided points to add (Can be improved, then Went badly, then
// Not in the plan; scoped by item, a Feature's item including its stories' and bugs' (not its action items), and by sprint; at most 100, newest first), the open action
// items, and the theme that most of the selected points share. Went well points and decided points are never offered or selected.
public sealed class RetroActionDialogReader(NpgsqlDataSource dataSource, IRetroCards cards, TimeProvider clock) : IRetroActionDialogReader
{
    private const int OfferedLimit = 100;

    public async Task<RetroActionDialogDto> GetAsync(Guid projectId, IReadOnlyList<Guid> pointIds, Guid? themeId, Guid? itemId, Guid? sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var given = pointIds.Distinct().ToArray();
        var selectedRows = (await connection.QueryAsync<PointKey>(new CommandDefinition(SelectedSql, new { projectId, given, themeId }, cancellationToken: cancellationToken))).ToList();
        var selectedIds = selectedRows.OrderBy(row => Array.IndexOf(given, row.Id) is var index and >= 0 ? index : int.MaxValue).Select(row => row.Id).ToList();
        var offeredRows = (await connection.QueryAsync<OfferedKey>(new CommandDefinition(OfferedSql, new { projectId, itemId, sprintId, selected = selectedIds.ToArray(), limit = OfferedLimit }, cancellationToken: cancellationToken))).ToList();
        var openActions = await OpenActionsAsync(connection, projectId, cancellationToken);

        var retroIds = selectedRows.Select(row => row.RetroId).Concat(offeredRows.Select(row => row.RetroId)).Distinct().ToList();
        var loaded = (await cards.LoadAsync(retroIds, cancellationToken))
            .SelectMany(card => card.Points)
            .ToDictionary(point => point.Id);
        var selected = selectedIds.Where(loaded.ContainsKey).Select(id => loaded[id]).ToList();
        var offered = offeredRows.Where(row => loaded.ContainsKey(row.Id)).OrderBy(row => CategoryRank(row.Category)).Select(row => loaded[row.Id]).ToList();
        return new RetroActionDialogDto(selected, offered, openActions, Commonest(selected));
    }

    // The theme most of the points have; a tie goes to the point listed first. Points with no theme are ignored.
    private static RetroThemeRefDto? Commonest(IReadOnlyList<RetroPointDto> points)
        => points.Where(point => point.Theme is not null)
            .GroupBy(point => point.Theme!.Id)
            .Select(group => (Theme: group.First().Theme!, Count: group.Count(), First: points.ToList().FindIndex(point => point.Theme?.Id == group.Key)))
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.First)
            .Select(entry => entry.Theme)
            .FirstOrDefault();

    private static int CategoryRank(string category) => category switch
    {
        "improve" => 0,
        "wentBadly" => 1,
        _ => 2,
    };

    private async Task<IReadOnlyList<RetroActionRefDto>> OpenActionsAsync(NpgsqlConnection connection, Guid projectId, CancellationToken cancellationToken)
    {
        var today = SprintLabels.Today(clock);
        var rows = await connection.QueryAsync<ActionRow>(new CommandDefinition(OpenActionSql, new { projectId }, cancellationToken: cancellationToken));
        return
        [
            .. rows.Select(row => new RetroActionRefDto(
                row.WorkItemId,
                row.Key,
                row.Title,
                new StateRef(row.StateId, row.StateName, Enum.Parse<StateCategory>(row.StateCategory, ignoreCase: true)),
                row.SprintId is { } sprintId
                    ? new SprintRef(sprintId, row.SprintName!, SprintLabels.Phase(DateOnly.ParseExact(row.SprintStart!, "yyyy-MM-dd", CultureInfo.InvariantCulture), DateOnly.ParseExact(row.SprintEnd!, "yyyy-MM-dd", CultureInfo.InvariantCulture), today).ToStatus())
                    : null,
                row.Points,
                row.FixType is { } fixType ? Enum.Parse<RetroFixType>(fixType, ignoreCase: true) : null,
                row.PointCount)),
        ];
    }

    private static readonly string SelectedSql = $"""
        SELECT p.id, p.retrospective_id AS retroId
        FROM retro_point p
        JOIN retrospective r ON r.id = p.retrospective_id
        WHERE r.project_id = @projectId AND r.archived_at IS NULL AND p.archived_at IS NULL AND p.category <> 'wentWell'
          AND {RetroDecisionSql.Undecided("p")}
          AND (p.id = ANY(@given) OR (@themeId::uuid IS NOT NULL AND p.theme_id = @themeId))
        ORDER BY p.created_at, p.id
        """;

    private static readonly string OfferedSql = $"""
        SELECT p.id, p.retrospective_id AS retroId, p.category
        FROM retro_point p
        JOIN retrospective r ON r.id = p.retrospective_id
        LEFT JOIN work_item w ON w.id = r.work_item_id
        WHERE r.project_id = @projectId AND r.archived_at IS NULL AND p.archived_at IS NULL AND p.category <> 'wentWell'
          AND {RetroDecisionSql.Undecided("p")}
          AND NOT (p.id = ANY(@selected))
          AND (@itemId::uuid IS NULL OR r.work_item_id = @itemId OR (w.parent_id = @itemId AND w.type IN ('story', 'bug')))
          AND (@sprintId::uuid IS NULL OR r.sprint_id = @sprintId)
        ORDER BY p.created_at DESC, p.id
        LIMIT @limit
        """;

    private static readonly string OpenActionSql = $"""
        SELECT aw.id AS workItemId, aw.key, aw.title, st.id AS stateId, st.name AS stateName, st.category AS stateCategory,
               asp.id AS sprintId, asp.name AS sprintName, to_char(asp.start_date, 'YYYY-MM-DD') AS sprintStart, to_char(asp.end_date, 'YYYY-MM-DD') AS sprintEnd,
               aw.story_points::numeric AS points, ra.fix_type AS fixType, {RetroActionSql.PointCount("ra")} AS pointCount
        FROM retro_action ra
        JOIN work_item aw ON aw.id = ra.work_item_id
        JOIN state st ON st.id = aw.state_id
        LEFT JOIN sprint asp ON asp.id = aw.sprint_id
        WHERE ra.project_id = @projectId AND st.category NOT IN ('completed', 'removed')
        ORDER BY aw.number DESC, aw.id
        """;

    private sealed record PointKey(Guid Id, Guid RetroId);

    private sealed record OfferedKey(Guid Id, Guid RetroId, string Category);

    private sealed record ActionRow(
        Guid WorkItemId, string Key, string Title, Guid StateId, string StateName, string StateCategory,
        Guid? SprintId, string? SprintName, string? SprintStart, string? SprintEnd, decimal? Points, string? FixType, int PointCount);
}
