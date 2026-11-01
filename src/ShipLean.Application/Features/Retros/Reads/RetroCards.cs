using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.Core;

namespace ShipLean.Application.Features.Retros.Reads;

// Retros as cards. A person's point (a point of a retro whose role is person) can be archived; an agent's never can.
// Recurring is false here, because the views that know the sprint history set it.
public sealed class RetroCards(NpgsqlDataSource dataSource, TimeProvider clock) : IRetroCards
{
    public async Task<IReadOnlyList<RetroCardDto>> LoadAsync(IReadOnlyList<Guid> retroIds, CancellationToken cancellationToken)
    {
        if (retroIds.Count == 0)
        {
            return [];
        }

        var ids = retroIds.ToArray();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var today = SprintLabels.Today(clock);
        var retros = (await connection.QueryAsync<RetroRow>(new CommandDefinition(RetroSql, new { ids }, cancellationToken: cancellationToken))).ToDictionary(row => row.Id);
        var points = (await connection.QueryAsync<PointRow>(new CommandDefinition(PointSql, new { ids }, cancellationToken: cancellationToken))).ToLookup(row => row.RetroId);
        var sources = (await connection.QueryAsync<SourceRow>(new CommandDefinition(SourceSql, new { ids }, cancellationToken: cancellationToken))).ToLookup(row => row.RetroId);
        var failures = (await connection.QueryAsync<FailureRow>(new CommandDefinition(FailureSql, new { ids }, cancellationToken: cancellationToken))).ToLookup(row => row.RetroId);

        return [.. retroIds.Where(retros.ContainsKey).Select(id => ToCard(retros[id], points[id], sources[id], failures[id], today, clock))];
    }

    private static RetroCardDto ToCard(RetroRow retro, IEnumerable<PointRow> points, IEnumerable<SourceRow> sources, IEnumerable<FailureRow> failures, DateOnly today, TimeProvider clock)
    {
        var role = ParseEnum<RetroRole>(retro.Role);
        var workItem = retro.ItemId is { } itemId ? new RetroItemRefDto(itemId, retro.ItemKey!, ToTypeRef(retro.ItemType!), retro.ItemTitle!) : null;
        var sprint = ToSprint(retro.SprintId, retro.SprintName, retro.SprintStart, retro.SprintEnd, today);
        var author = new RetroAuthorDto(new UserRef(retro.UserId, retro.UserName, retro.UserInitials, retro.UserColour, retro.UserPhotoUrl), retro.AgentName, role);
        var isFeature = retro.ItemType == "feature";
        var dtos = points.Select(point => ToPoint(point, role, workItem, sprint, isFeature, today)).ToList();
        var log = failures.ToList();
        var createdAt = new DateTimeOffset(DateTime.SpecifyKind(retro.CreatedAt, DateTimeKind.Utc));
        return new RetroCardDto(
            retro.Id,
            workItem,
            sprint,
            author,
            retro.Partial,
            createdAt,
            DisplayDate.Relative(createdAt, clock),
            [.. sources.Select(source => new RetroItemRefDto(source.ItemId, source.ItemKey, ToTypeRef(source.ItemType), source.ItemTitle))],
            dtos,
            new RetroFailureLogDto(
                [.. log.Where(row => row.Kind == "blocker").Select(row => row.Text)],
                [.. log.Where(row => row.Kind == "assumptionDisproved").Select(row => row.Text)],
                [.. log.Where(row => row.Kind == "friction").Select(row => row.Text)]));
    }

    private static RetroPointDto ToPoint(PointRow point, RetroRole role, RetroItemRefDto? workItem, SprintRef? sprint, bool isFeature, DateOnly today)
    {
        var theme = point.ThemeId is { } themeId ? new RetroThemeRefDto(themeId, point.ThemeName!, ParseEnum<RetroTone>(point.ThemeTone!)) : null;
        RetroActionRefDto? action = null;
        if (point.ActionId is { } actionId && point.ActionStateCategory != "removed")
        {
            var state = new StateRef(point.ActionStateId!.Value, point.ActionStateName!, ParseEnum<StateCategory>(point.ActionStateCategory!));
            var actionSprint = ToSprint(point.ActionSprintId, point.ActionSprintName, point.ActionSprintStart, point.ActionSprintEnd, today);
            action = new RetroActionRefDto(actionId, point.ActionKey!, point.ActionTitle!, state, actionSprint, point.ActionPoints);
        }

        var author = new RetroAuthorDto(new UserRef(point.UserId, point.UserName, point.UserInitials, point.UserColour, point.UserPhotoUrl), point.AgentName, role);
        return new RetroPointDto(
            point.Id,
            point.RetroId,
            ParseEnum<RetroCategory>(point.Category),
            point.Text,
            point.RootCause,
            theme,
            point.ThemeSetBy is { } setBy ? ParseEnum<RetroThemeSetBy>(setBy) : null,
            author,
            workItem,
            sprint,
            isFeature,
            false,
            action,
            role == RetroRole.Person);
    }

    private static SprintRef? ToSprint(Guid? id, string? name, string? start, string? end, DateOnly today)
    {
        if (id is not { } sprintId)
        {
            return null;
        }

        var phase = SprintLabels.Phase(DateOnly.ParseExact(start!, "yyyy-MM-dd", CultureInfo.InvariantCulture), DateOnly.ParseExact(end!, "yyyy-MM-dd", CultureInfo.InvariantCulture), today);
        return new SprintRef(sprintId, name!, phase.ToStatus());
    }

    private static TypeRef ToTypeRef(string type)
    {
        var id = ParseEnum<WorkItemTypeId>(type);
        return new TypeRef(id, WorkItemTypeRules.Name(id));
    }

    private static T ParseEnum<T>(string value)
        where T : struct, Enum
        => Enum.Parse<T>(value, ignoreCase: true);

    private static readonly string UserColumns = $"u.id AS userId, u.name AS userName, u.initials AS userInitials, u.avatar_colour AS userColour, {UserRefs.PhotoUrlSql} AS userPhotoUrl";

    private static readonly string RetroSql = $"""
        SELECT r.id, r.role, r.agent_name AS agentName, r.partial, r.created_at AS createdAt,
               {UserColumns},
               w.id AS itemId, w.key AS itemKey, w.type AS itemType, w.title AS itemTitle,
               sp.id AS sprintId, sp.name AS sprintName, to_char(sp.start_date, 'YYYY-MM-DD') AS sprintStart, to_char(sp.end_date, 'YYYY-MM-DD') AS sprintEnd
        FROM retrospective r
        JOIN app_user u ON u.id = r.author_user_id
        LEFT JOIN work_item w ON w.id = r.work_item_id
        LEFT JOIN sprint sp ON sp.id = r.sprint_id
        WHERE r.id = ANY(@ids)
        """;

    private static readonly string PointSql = $"""
        SELECT p.id, p.retrospective_id AS retroId, p.category, p.text, p.root_cause AS rootCause, p.theme_set_by AS themeSetBy, p.agent_name AS agentName, p.position,
               {UserColumns},
               t.id AS themeId, t.name AS themeName, t.tone AS themeTone,
               aw.id AS actionId, aw.key AS actionKey, aw.title AS actionTitle, st.id AS actionStateId, st.name AS actionStateName, st.category AS actionStateCategory,
               asp.id AS actionSprintId, asp.name AS actionSprintName, to_char(asp.start_date, 'YYYY-MM-DD') AS actionSprintStart, to_char(asp.end_date, 'YYYY-MM-DD') AS actionSprintEnd,
               aw.story_points::numeric AS actionPoints
        FROM retro_point p
        JOIN app_user u ON u.id = p.created_by_user_id
        LEFT JOIN retro_theme t ON t.id = p.theme_id
        LEFT JOIN retro_action ra ON ra.point_id = p.id
        LEFT JOIN work_item aw ON aw.id = ra.work_item_id
        LEFT JOIN state st ON st.id = aw.state_id
        LEFT JOIN sprint asp ON asp.id = aw.sprint_id
        WHERE p.retrospective_id = ANY(@ids) AND p.archived_at IS NULL
        ORDER BY p.position, p.created_at, p.id
        """;

    private const string SourceSql = """
        SELECT s.retrospective_id AS retroId, w.id AS itemId, w.key AS itemKey, w.type AS itemType, w.title AS itemTitle
        FROM retrospective_source s
        JOIN retrospective src ON src.id = s.source_retrospective_id
        JOIN work_item w ON w.id = src.work_item_id
        WHERE s.retrospective_id = ANY(@ids)
        ORDER BY w.number, w.id
        """;

    private const string FailureSql = """
        SELECT retrospective_id AS retroId, kind, text FROM retro_failure_log
        WHERE retrospective_id = ANY(@ids)
        ORDER BY position, created_at, id
        """;

    private sealed record RetroRow(Guid Id, string Role, string? AgentName, bool Partial, DateTime CreatedAt, Guid UserId, string UserName, string UserInitials, string UserColour, string? UserPhotoUrl, Guid? ItemId, string? ItemKey, string? ItemType, string? ItemTitle, Guid? SprintId, string? SprintName, string? SprintStart, string? SprintEnd);

    private sealed record PointRow(
        Guid Id, Guid RetroId, string Category, string Text, string? RootCause, string? ThemeSetBy, string? AgentName, int Position,
        Guid UserId, string UserName, string UserInitials, string UserColour, string? UserPhotoUrl,
        Guid? ThemeId, string? ThemeName, string? ThemeTone,
        Guid? ActionId, string? ActionKey, string? ActionTitle, Guid? ActionStateId, string? ActionStateName, string? ActionStateCategory,
        Guid? ActionSprintId, string? ActionSprintName, string? ActionSprintStart, string? ActionSprintEnd, decimal? ActionPoints);

    private sealed record SourceRow(Guid RetroId, Guid ItemId, string ItemKey, string ItemType, string ItemTitle);

    private sealed record FailureRow(Guid RetroId, string Kind, string Text);
}
