using System.Data.Common;
using Dapper;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Sprints;

public sealed partial class SprintStore
{
    private const decimal LargestCapacity = 9999.99m;

    public async Task<SprintCapacityDto> GetCapacityAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await LoadAsync(connection, null, sprintId, forUpdate: false, cancellationToken);
        return await ReadCapacityAsync(connection, null, sprintId, cancellationToken);
    }

    // The entered set replaces the old one: a member left out has their row archived, one listed again revives it.
    public async Task<SprintCapacityDto> PutCapacityAsync(Guid sprintId, SprintCapacityPut capacities, CancellationToken cancellationToken = default)
    {
        if (capacities?.Members is null || capacities.Members.Any(m => m is null))
        {
            throw ApiException.Validation("Capacity needs its list of members.");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var sprint = await LoadAsync(connection, transaction, sprintId, forUpdate: true, cancellationToken);
        await CheckCapacityAsync(connection, transaction, sprint.ProjectId, capacities, cancellationToken);
        var now = clock.GetUtcNow();
        var listed = capacities.Members.Select(m => m.UserId).ToArray();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE sprint_capacity SET archived_at = @now, updated_at = @now WHERE sprint_id = @sprintId AND archived_at IS NULL AND NOT (user_id = ANY(@listed))",
            new { sprintId, listed, now }, transaction, cancellationToken: cancellationToken));
        foreach (var row in capacities.Members)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO sprint_capacity (id, sprint_id, user_id, capacity_points, note, created_at, updated_at)
                VALUES (@id, @sprintId, @userId, @points, @note, @now, @now)
                ON CONFLICT (sprint_id, user_id) DO UPDATE
                SET capacity_points = EXCLUDED.capacity_points, note = EXCLUDED.note, archived_at = NULL, updated_at = EXCLUDED.updated_at
                """,
                new { id = Guid.NewGuid(), sprintId, userId = row.UserId, points = row.Points, note = row.Note, now }, transaction, cancellationToken: cancellationToken));
        }

        var saved = await ReadCapacityAsync(connection, transaction, sprintId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new SprintChanged(sprint.ProjectId, sprintId));
        return saved;
    }

    private static async Task CheckCapacityAsync(DbConnection connection, DbTransaction transaction, Guid projectId, SprintCapacityPut capacities, CancellationToken cancellationToken)
    {
        if (capacities.Members.Any(m => m.Points is < 0 or > LargestCapacity))
        {
            throw ApiException.Validation("Capacity is zero or more points for each member.");
        }

        if (capacities.Members.Any(m => decimal.Round(m.Points, 2) != m.Points))
        {
            throw ApiException.Validation("Capacity has at most two decimal places.");
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

    private static async Task<SprintCapacityDto> ReadCapacityAsync(DbConnection connection, DbTransaction? transaction, Guid sprintId, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<CapacityRow>(new CommandDefinition(
            $"""
            SELECT {UserRefs.Columns}, c.capacity_points AS points, c.note
            FROM sprint_capacity c JOIN app_user u ON u.id = c.user_id
            WHERE c.sprint_id = @sprintId AND c.archived_at IS NULL ORDER BY u.name, u.id
            """,
            new { sprintId }, transaction, cancellationToken: cancellationToken));
        var members = rows.Select(r => r.ToDto()).ToList();
        return new SprintCapacityDto(sprintId, members, members.Sum(m => m.Points));
    }
}
