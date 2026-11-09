using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.DeviceSessions;

namespace ShipLean.Application.Features.DeviceSessions;

// IDeviceSessions over Postgres. Codes and secrets are stored only as SHA-256 hashes. Every rule is decided by one
// conditional UPDATE ... RETURNING, so concurrent callers cannot both win: of two exchanges of one code, only one UPDATE
// finds used_at still null. The verifier is checked after the code is claimed, so a wrong verifier spends it.
public sealed class DeviceSessions(NpgsqlDataSource dataSource) : IDeviceSessions
{
    private const string Columns = "id, user_id AS userId, name, token_prefix AS tokenPrefix, security_stamp AS securityStamp, created_at AS createdAt, last_used_at AS lastUsedAt, expires_at AS expiresAt, revoked_at AS revokedAt";

    private sealed record SessionRow(
        Guid Id, Guid UserId, string Name, string TokenPrefix, string SecurityStamp,
        DateTimeOffset CreatedAt, DateTimeOffset LastUsedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt)
    {
        public DeviceSessionRecord ToRecord() => new(Id, UserId, Name, TokenPrefix, SecurityStamp, CreatedAt, LastUsedAt, ExpiresAt, RevokedAt);
    }

    private sealed record ClaimedCode(Guid UserId, string SecurityStamp, string Challenge, string DeviceName);

    public async Task<string> CreateCodeAsync(Guid userId, string securityStamp, string challenge, string deviceName, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var code = DeviceSecret.NewCode();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO device_sign_in_code (id, code_hash, challenge, user_id, security_stamp, device_name, created_at, expires_at)
            VALUES (@id, @hash, @challenge, @userId, @securityStamp, @deviceName, @now, @expiresAt)
            """,
            new
            {
                id = Guid.CreateVersion7(now), hash = DeviceSecret.Hash(code), challenge, userId, securityStamp, deviceName, now,
                expiresAt = now + DeviceSessionLimits.CodeLifetime,
            },
            cancellationToken: cancellationToken));
        return code;
    }

    public async Task<DeviceSignIn?> ExchangeAsync(string code, string verifier, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // The claim: the one statement that decides a race. Whoever sets used_at first gets the row back; everyone else gets nothing.
        var claimed = await connection.QuerySingleOrDefaultAsync<ClaimedCode>(new CommandDefinition(
            """
            UPDATE device_sign_in_code SET used_at = @now
            WHERE code_hash = @hash AND used_at IS NULL AND expires_at > @now
            RETURNING user_id AS userId, security_stamp AS securityStamp, challenge, device_name AS deviceName
            """,
            new { hash = DeviceSecret.Hash(code), now }, transaction, cancellationToken: cancellationToken));
        if (claimed is null)
        {
            return null;
        }
        if (!DeviceSecret.VerifierMatches(verifier, claimed.Challenge))
        {
            // Commit: the code stays spent.
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var secret = DeviceSecret.New();
        // Read back what Postgres stored (microsecond times), so the returned session equals what FindBySecret returns.
        var row = await connection.QuerySingleAsync<SessionRow>(new CommandDefinition(
            $"""
            INSERT INTO device_session (id, user_id, name, token_hash, token_prefix, security_stamp, created_at, last_used_at, expires_at)
            VALUES (@id, @UserId, @name, @hash, @tokenPrefix, @SecurityStamp, @now, @now, @expiresAt)
            RETURNING {Columns}
            """,
            new
            {
                id = Guid.CreateVersion7(now), claimed.UserId, name = claimed.DeviceName, hash = DeviceSecret.Hash(secret),
                tokenPrefix = DeviceSecret.TokenPrefix(secret), claimed.SecurityStamp, now, expiresAt = now + DeviceSessionLimits.SessionLifetime,
            },
            transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return new DeviceSignIn(row.ToRecord(), secret);
    }

    public async Task<DeviceSessionRecord?> FindBySecretAsync(string secret, CancellationToken cancellationToken = default)
    {
        var hash = DeviceSecret.Hash(secret);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(new CommandDefinition(
            $"SELECT {Columns} FROM device_session WHERE token_hash = @hash",
            new { hash }, cancellationToken: cancellationToken));
        return row?.ToRecord();
    }

    public async Task TouchAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE device_session SET last_used_at = @now, expires_at = @expiresAt
            WHERE id = @id AND revoked_at IS NULL AND expires_at > @now AND last_used_at <= @threshold
            """,
            new { id, now, expiresAt = now + DeviceSessionLimits.SessionLifetime, threshold = now - DeviceSessionLimits.TouchInterval },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DeviceSessionRecord>> ListAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SessionRow>(new CommandDefinition(
            $"SELECT {Columns} FROM device_session WHERE user_id = @userId AND revoked_at IS NULL AND expires_at > @now ORDER BY created_at DESC, id DESC",
            new { userId, now }, cancellationToken: cancellationToken));
        return rows.Select(r => r.ToRecord()).ToList();
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid id, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE device_session SET revoked_at = @now WHERE id = @id AND user_id = @userId AND revoked_at IS NULL",
            new { id, userId, now }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task<bool> EndAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE device_session SET revoked_at = @now WHERE id = @id AND revoked_at IS NULL",
            new { id, now }, cancellationToken: cancellationToken)) == 1;
    }
}
