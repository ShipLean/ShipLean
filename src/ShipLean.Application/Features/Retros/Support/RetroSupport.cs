using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Retros.Support;

// A +1 is a row in retro_point_support; withdrawing stamps withdrawn_at and never deletes, so supporting again adds a new row.
// Only one row per person and point is live (the partial unique index), which makes both calls idempotent.
public sealed class RetroSupport(NpgsqlDataSource dataSource, ICurrentUser user, TimeProvider clock) : IRetroSupport
{
    public async Task SupportAsync(Guid pointId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireLivePointAsync(connection, pointId, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO retro_point_support (id, point_id, user_id, created_at)
            VALUES (@id, @pointId, @userId, @now)
            ON CONFLICT (point_id, user_id) WHERE withdrawn_at IS NULL DO NOTHING
            """,
            new { id = Guid.NewGuid(), pointId, userId = user.Id, now = clock.GetUtcNow() },
            cancellationToken: cancellationToken));
    }

    public async Task WithdrawAsync(Guid pointId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireLivePointAsync(connection, pointId, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE retro_point_support SET withdrawn_at = @now WHERE point_id = @pointId AND user_id = @userId AND withdrawn_at IS NULL",
            new { pointId, userId = user.Id, now = clock.GetUtcNow() },
            cancellationToken: cancellationToken));
    }

    private static async Task RequireLivePointAsync(NpgsqlConnection connection, Guid pointId, CancellationToken cancellationToken)
    {
        var live = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM retro_point WHERE id = @pointId AND archived_at IS NULL)",
            new { pointId },
            cancellationToken: cancellationToken));
        if (!live)
        {
            throw ApiException.NotFound("That retro point doesn't exist.");
        }
    }
}
