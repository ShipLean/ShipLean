using System.Data.Common;
using Dapper;
using ShipLean.Application.Identity;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Features.Sprints;

public sealed partial class SprintStore
{
    private const decimal LargestCapacity = CapacityPoints.Largest;

    public async Task<SprintCapacityDto> GetCapacityAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sprint = await LoadAsync(connection, null, sprintId, forUpdate: false, cancellationToken);
        return await ReadCapacityAsync(connection, null, sprint, cancellationToken);
    }

    // The entered set replaces the old one: a member left out has their row archived, one listed again revives it. The server
    // computes each member's points from the velocity and days; an ended sprint's capacity can't change.
    public async Task<SprintCapacityDto> PutCapacityAsync(Guid sprintId, SprintCapacityPut capacities, CancellationToken cancellationToken = default)
    {
        if (capacities?.Members is null || capacities.Members.Any(m => m is null))
        {
            throw ApiException.Validation("Capacity needs its list of members.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var sprint = await LoadAsync(connection, transaction, sprintId, forUpdate: true, cancellationToken);
        if (sprint.End < SprintLabels.Today(clock))
        {
            throw ApiException.Validation("An ended sprint's capacity can't change.");
        }

        var project = await CapacityProject.LoadAsync(connection, transaction, sprint.ProjectId, cancellationToken);
        var calendar = await CapacityCalendar.LoadAsync(holidays, project.HolidayCountry, project.Days, sprint.Start, sprint.End, cancellationToken);
        await CheckCapacityAsync(connection, transaction, sprint.ProjectId, capacities, calendar, cancellationToken);
        var now = clock.GetUtcNow();
        if (capacities.Velocity is { } velocity)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE sprint SET capacity_velocity = @velocity, updated_at = @now WHERE id = @sprintId",
                new { sprintId, velocity, now }, transaction, cancellationToken: cancellationToken));
        }

        var listed = capacities.Members.Select(m => m.UserId).ToArray();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE sprint_capacity SET archived_at = @now, updated_at = @now WHERE sprint_id = @sprintId AND archived_at IS NULL AND NOT (user_id = ANY(@listed))",
            new { sprintId, listed, now }, transaction, cancellationToken: cancellationToken));
        foreach (var row in capacities.Members)
        {
            var points = CapacityPoints.For(capacities.Velocity ?? 0m, capacities.Members.Count, calendar.Effective(row.Days), calendar.WorkingDays);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO sprint_capacity (id, sprint_id, user_id, capacity_points, days, note, created_at, updated_at)
                VALUES (@id, @sprintId, @userId, @points, @days, @note, @now, @now)
                ON CONFLICT (sprint_id, user_id) DO UPDATE
                SET capacity_points = EXCLUDED.capacity_points, days = EXCLUDED.days, note = EXCLUDED.note, archived_at = NULL, updated_at = EXCLUDED.updated_at
                """,
                new { id = Guid.NewGuid(), sprintId, userId = row.UserId, points, days = row.Days, note = row.Note, now }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new SprintChanged(sprint.ProjectId, sprintId));
        return await ReadCapacityAsync(connection, null, sprint, cancellationToken);
    }

    private static async Task CheckCapacityAsync(
        DbConnection connection, DbTransaction transaction, Guid projectId, SprintCapacityPut capacities, CapacityCalendar calendar, CancellationToken cancellationToken)
    {
        if (capacities.Members.Count > 0 && capacities.Velocity is null)
        {
            throw ApiException.Validation("Capacity needs a velocity.");
        }

        if (capacities.Velocity is < 0 or > LargestCapacity)
        {
            throw ApiException.Validation("Velocity is zero or more points, up to 9999.99.");
        }

        if (capacities.Velocity is { } velocity && decimal.Round(velocity, 2) != velocity)
        {
            throw ApiException.Validation("Velocity has at most two decimal places.");
        }

        if (capacities.Members.Any(m => m.Days is { } days && (days < 0 || days > calendar.WorkingDays || days * 2 != decimal.Truncate(days * 2))))
        {
            throw ApiException.Validation($"Days are between 0 and {calendar.WorkingDays}, in steps of half a day.");
        }

        var users = capacities.Members.Select(m => m.UserId).ToArray();
        if (users.Distinct().Count() != users.Length)
        {
            throw ApiException.Validation("A member can only be listed once.");
        }

        var members = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM project_member WHERE project_id = @projectId AND archived_at IS NULL AND user_id = ANY(@users)",
            new { projectId, users }, transaction, cancellationToken: cancellationToken));
        if (members != users.Length)
        {
            throw ApiException.Validation("Capacity can only be entered for members of the project.");
        }
    }

    private async Task<SprintCapacityDto> ReadCapacityAsync(DbConnection connection, DbTransaction? transaction, SprintRow sprint, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<CapacityRow>(new CommandDefinition(
            $"""
            SELECT {UserRefs.Columns}, c.days, c.capacity_points AS points, c.note
            FROM sprint_capacity c JOIN app_user u ON u.id = c.user_id
            WHERE c.sprint_id = @sprintId AND c.archived_at IS NULL ORDER BY u.name, u.id
            """,
            new { sprintId = sprint.Id }, transaction, cancellationToken: cancellationToken));
        var members = rows.Select(r => r.ToDto()).ToList();
        var stored = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            "SELECT capacity_velocity FROM sprint WHERE id = @id", new { id = sprint.Id }, transaction, cancellationToken: cancellationToken));
        var project = await CapacityProject.LoadAsync(connection, transaction, sprint.ProjectId, cancellationToken);
        var calendar = await CapacityCalendar.LoadAsync(holidays, project.HolidayCountry, project.Days, sprint.Start, sprint.End, cancellationToken);
        var previous = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            """
            SELECT capacity_velocity FROM sprint
            WHERE project_id = @projectId AND archived_at IS NULL AND capacity_velocity IS NOT NULL AND start_date < @start
            ORDER BY start_date DESC LIMIT 1
            """,
            new { projectId = sprint.ProjectId, start = sprint.Start }, transaction, cancellationToken: cancellationToken));
        var fallback = previous ?? project.VelocityOverride ?? (await metrics.GetVelocityAsync(sprint.ProjectId, cancellationToken)).Average;
        var velocityDefault = Math.Min(LargestCapacity, decimal.Round(fallback, 1, MidpointRounding.AwayFromZero));
        return new SprintCapacityDto(
            sprint.Id, members, members.Sum(m => m.Points), stored, velocityDefault, calendar.WorkingDays, calendar.DefaultDays,
            calendar.Holidays, project.HolidayCountry, sprint.End < SprintLabels.Today(clock));
    }
}
