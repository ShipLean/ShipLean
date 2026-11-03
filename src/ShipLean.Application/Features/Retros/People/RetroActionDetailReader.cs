using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Reads;
using ShipLean.Application.Sprints;
using ShipLean.Core;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// The action item as the writes reply with it: its ref, its theme, the sprint it was raised in and its live points. Read after the write committed.
internal sealed class RetroActionDetailReader(NpgsqlDataSource dataSource, IRetroCards cards, TimeProvider clock)
{
    public async Task<RetroActionDetailDto> LoadAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ActionRow>(new CommandDefinition(ActionSql, new { workItemId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No action item with that id was found.");
        var pointRows = (await connection.QueryAsync<(Guid RetroId, Guid PointId)>(new CommandDefinition(PointSql, new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var loaded = await cards.LoadAsync([.. pointRows.Select(point => point.RetroId).Distinct()], cancellationToken);
        var byId = loaded.SelectMany(card => card.Points).ToDictionary(point => point.Id);
        // The cards are read on another connection, after the link rows: keep a point only while the cards still say this action is its live one (K1).
        var points = pointRows.Select(point => byId.GetValueOrDefault(point.PointId)).OfType<RetroPointDto>().Where(point => point.Action?.WorkItemId == workItemId).ToList();

        var today = SprintLabels.Today(clock);
        var state = new StateRef(row.StateId, row.StateName, Enum.Parse<StateCategory>(row.StateCategory, ignoreCase: true));
        var action = new RetroActionRefDto(
            row.WorkItemId, row.Key, row.Title, state, Sprint(row.SprintId, row.SprintName, row.SprintStart, row.SprintEnd, today), row.Points,
            row.FixType is { } fixType ? Enum.Parse<RetroFixType>(fixType, ignoreCase: true) : null, points.Count);
        var theme = row.ThemeId is { } themeId ? new RetroThemeRefDto(themeId, row.ThemeName!, Enum.Parse<RetroTone>(row.ThemeTone!, ignoreCase: true)) : null;
        return new RetroActionDetailDto(action, theme, points, Sprint(row.RaisedId, row.RaisedName, row.RaisedStart, row.RaisedEnd, today));
    }

    private static SprintRef? Sprint(Guid? id, string? name, string? start, string? end, DateOnly today)
        => id is { } sprintId
            ? new SprintRef(sprintId, name!, SprintLabels.Phase(DateOnly.ParseExact(start!, "yyyy-MM-dd", CultureInfo.InvariantCulture), DateOnly.ParseExact(end!, "yyyy-MM-dd", CultureInfo.InvariantCulture), today).ToStatus())
            : null;

    private static readonly string ActionSql = $"""
        SELECT aw.id AS workItemId, aw.key, aw.title, st.id AS stateId, st.name AS stateName, st.category AS stateCategory,
               asp.id AS sprintId, asp.name AS sprintName, to_char(asp.start_date, 'YYYY-MM-DD') AS sprintStart, to_char(asp.end_date, 'YYYY-MM-DD') AS sprintEnd,
               aw.story_points::numeric AS points, ra.fix_type AS fixType,
               t.id AS themeId, t.name AS themeName, t.tone AS themeTone,
               rs.id AS raisedId, rs.name AS raisedName, to_char(rs.start_date, 'YYYY-MM-DD') AS raisedStart, to_char(rs.end_date, 'YYYY-MM-DD') AS raisedEnd
        FROM retro_action ra
        JOIN work_item aw ON aw.id = ra.work_item_id
        JOIN state st ON st.id = aw.state_id
        LEFT JOIN sprint asp ON asp.id = aw.sprint_id
        LEFT JOIN retro_theme t ON t.id = ra.theme_id
        LEFT JOIN sprint rs ON rs.id = ra.raised_sprint_id
        WHERE ra.work_item_id = @workItemId
        """;

    private const string PointSql = """
        SELECT p.retrospective_id AS retroId, p.id AS pointId
        FROM retro_action_point rap JOIN retro_point p ON p.id = rap.point_id
        WHERE rap.work_item_id = @workItemId AND rap.archived_at IS NULL AND p.archived_at IS NULL
        ORDER BY rap.position, rap.created_at, p.id
        """;

    private sealed record ActionRow(
        Guid WorkItemId, string Key, string Title, Guid StateId, string StateName, string StateCategory,
        Guid? SprintId, string? SprintName, string? SprintStart, string? SprintEnd, decimal? Points, string? FixType,
        Guid? ThemeId, string? ThemeName, string? ThemeTone,
        Guid? RaisedId, string? RaisedName, string? RaisedStart, string? RaisedEnd);
}
