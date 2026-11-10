using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Builders;

// Arranges one +1 on a retro point (retro_point_support) with parameterised SQL and returns its id. The default is a live support
// made now; WithWithdrawnAt arranges one already withdrawn.
public sealed class RetroSupportBuilder(Guid pointId, Guid userId)
{
    private readonly Guid _id = Guid.NewGuid();
    private DateTimeOffset? _createdAt;
    private DateTimeOffset? _withdrawnAt;

    public RetroSupportBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public RetroSupportBuilder WithWithdrawnAt(DateTimeOffset withdrawnAt)
    {
        _withdrawnAt = withdrawnAt;
        return this;
    }

    public async Task<Guid> InsertAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO retro_point_support (id, point_id, user_id, created_at, withdrawn_at)
            VALUES (@id, @pointId, @userId, coalesce(@createdAt::timestamptz, now()), @withdrawnAt::timestamptz)
            """,
            new { id = _id, pointId, userId, createdAt = _createdAt?.ToUniversalTime(), withdrawnAt = _withdrawnAt?.ToUniversalTime() });
        return _id;
    }
}
