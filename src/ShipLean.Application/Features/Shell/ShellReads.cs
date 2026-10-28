using System.Text.Json;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Application.Sprints;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.Users;
using ShipLean.Core;

namespace ShipLean.Application.Features.Shell;

// The reads every page header and the picker use, by SQL over the database. Callers check IProjectAccess first, except
// ProjectsAsync (it filters by the caller's own memberships) and ByKeyAsync (it resolves through the caller's membership).
[RegisterPerRequest]
public sealed class ShellReads(NpgsqlDataSource dataSource, ICurrentUser currentUser, TimeProvider clock)
{
    private const string ProjectColumns =
        """
        p.id, p.key, p.name, p.description, m.role AS roleText,
        (SELECT count(*) FROM project_member x WHERE x.project_id = p.id AND x.archived_at IS NULL)::int AS memberCount,
        array_to_string(p.working_days, ',') AS workingDays, p.velocity_window AS velocityWindow, p.velocity_override AS velocityOverride,
        p.next_item_number AS nextItemNumber
        """;

    private const string MyMembership = "JOIN project_member m ON m.project_id = p.id AND m.user_id = @me AND m.archived_at IS NULL";

    public async Task<IReadOnlyList<ProjectSummaryDto>> ProjectsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<ProjectRow>(new CommandDefinition(
            $"SELECT {ProjectColumns} FROM project p {MyMembership} ORDER BY p.name", new { me = currentUser.Id }, cancellationToken: cancellationToken))).ToList();
        var recent = await RecentAsync(connection, cancellationToken);
        return [.. rows
            .OrderBy(row => recent.IndexOf(row.Id) is var at and >= 0 ? at : int.MaxValue)
            .Select(row => new ProjectSummaryDto(row.Id, row.Key, row.Name, row.Role, row.MemberCount))];
    }

    public async Task<ProjectDto> ByKeyAsync(string key, IProjectAccess access, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProjectRow>(new CommandDefinition(
            $"SELECT {ProjectColumns} FROM project p {MyMembership} WHERE p.key = @key", new { me = currentUser.Id, key = key.ToUpperInvariant() },
            cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("There is no project with that key.");
        await access.RequireAsync(row.Id, ProjectRole.Member, cancellationToken);
        return row.ToDto();
    }

    public async Task<ProjectDto> ProjectAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<ProjectRow>(new CommandDefinition(
            $"SELECT {ProjectColumns} FROM project p {MyMembership} WHERE p.id = @id", new { me = currentUser.Id, id },
            cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("There is no such project.");
        return row.ToDto();
    }

    public async Task<IReadOnlyList<SprintDto>> SprintsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SprintRow>(new CommandDefinition(
            """
            SELECT id, name, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate, goal, rollover_status AS rolloverStatus
            FROM sprint WHERE project_id = @projectId AND archived_at IS NULL ORDER BY start_date, name
            """,
            new { projectId }, cancellationToken: cancellationToken));
        var today = SprintLabels.Today(clock);
        return [.. rows.Select(row => row.ToDto(today))];
    }

    public async Task<IReadOnlyList<MemberDto>> MembersAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<MemberRow>(new CommandDefinition(
            $"""
            SELECT {UserRefs.Columns}, u.email, m.role,
                   {UserStatusRule.Sql} AS status
            FROM project_member m JOIN app_user u ON u.id = m.user_id
            WHERE m.project_id = @projectId AND m.archived_at IS NULL ORDER BY u.name
            """,
            new { projectId, now = clock.GetUtcNow().UtcDateTime }, cancellationToken: cancellationToken));
        return [.. rows.Select(row => row.ToDto())];
    }

    public async Task<IReadOnlyList<StateDto>> StatesAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<StateRow>(new CommandDefinition(
            """
            SELECT s.id, s.type, s.name, s.category, s.sort_order AS "order",
                   (SELECT count(*) FROM work_item w WHERE w.state_id = s.id)::int AS itemCount, s.starts_agent AS startsAgent
            FROM state s WHERE s.project_id = @projectId AND s.archived_at IS NULL
            ORDER BY array_position(ARRAY['epic', 'feature', 'story', 'bug', 'task'], s.type), s.sort_order
            """,
            new { projectId }, cancellationToken: cancellationToken));
        return [.. rows.Select(row => new StateDto(row.Id, Enum.Parse<WorkItemTypeId>(row.Type, true), row.Name, Enum.Parse<StateCategory>(row.Category, true), row.Order, row.ItemCount, row.StartsAgent))];
    }

    private async Task<List<Guid>> RecentAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var json = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT json::text FROM user_preference WHERE user_id = @me AND key = 'recentProjects'", new { me = currentUser.Id }, cancellationToken: cancellationToken));
        if (json is null)
        {
            return [];
        }
        using var document = JsonDocument.Parse(json);
        var element = document.RootElement;
        return element.ValueKind == JsonValueKind.Array
            ? [.. element.EnumerateArray().Select(v => Guid.TryParse(v.GetString(), out var id) ? id : Guid.Empty)]
            : [];
    }

    private sealed record ProjectRow(
        Guid Id, string Key, string Name, string Description, string RoleText, int MemberCount, string WorkingDays,
        int VelocityWindow, decimal? VelocityOverride, int NextItemNumber)
    {
        public ProjectRole Role => Enum.Parse<ProjectRole>(RoleText, true);

        public ProjectDto ToDto() => new(Id, Key, Name, Description, Role, MemberCount, [.. WorkingDays.Split(',').Select(int.Parse).Order()], VelocityWindow, VelocityOverride, NextItemNumber);
    }

    private sealed record SprintRow(Guid Id, string Name, string StartDate, string EndDate, string Goal, string? RolloverStatus)
    {
        public SprintDto ToDto(DateOnly today)
        {
            var start = DateOnly.ParseExact(StartDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            var end = DateOnly.ParseExact(EndDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            return new SprintDto(
                Id, Name, start, end, Goal, SprintLabels.Phase(start, end, today).ToStatus(),
                RolloverStatus is null ? null : Enum.Parse<Contracts.Sprints.RolloverStatus>(RolloverStatus, true),
                SprintLabels.Dates(start, end, today), SprintLabels.Time(start, end, today));
        }
    }

    private sealed record MemberRow(
        Guid Id, string Name, string Initials, string AvatarColour, string? PhotoUrl, string Email, string Role,
        string Status)
    {
        public MemberDto ToDto() => new(
            new UserRef(Id, Name, Initials, AvatarColour, PhotoUrl), Email, Enum.Parse<ProjectRole>(Role, true),
            UserStatusRule.Parse(Status));
    }

    private sealed record StateRow(Guid Id, string Type, string Name, string Category, int Order, int ItemCount, bool StartsAgent);
}
