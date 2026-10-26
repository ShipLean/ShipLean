using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Stores;

// oauth_token: access (1 hour) and refresh (90 days from last use) tokens by SHA-256 hash, in families. A refresh token
// is spent by rotation; presenting a spent one revokes its family. The refresh row is locked while it is judged, so
// concurrent refreshes of one token serialise and every loser trips the reuse rule.
public sealed class AgentTokenIssuer(NpgsqlDataSource dataSource, TimeProvider clock) : IAgentTokenIssuer
{
    public static readonly TimeSpan AccessLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(90);

    private sealed record Refresh(
        Guid AuthorizationId,
        Guid FamilyId,
        string Resource,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RotatedAt,
        DateTimeOffset? RevokedAt,
        string ClientId,
        DateTimeOffset? AuthorizationRevokedAt,
        string AuthorizationStamp,
        string UserStamp,
        bool UserDisabled,
        Array Scopes)
    {
    }

    private sealed record Issuable(
        Array Scopes,
        string AuthorizationStamp,
        string UserStamp,
        bool UserDisabled)
    {
    }

    private sealed record Spent(
        Guid AuthorizationId,
        Guid FamilyId,
        string Kind,
        string ClientId)
    {
    }

    public async Task<AgentTokenPair> IssueAsync(Guid authorizationId, string resource, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var authorization = await connection.QuerySingleOrDefaultAsync<Issuable>(new CommandDefinition(
            """
            SELECT a.scopes, a.security_stamp AS authorizationStamp, u.security_stamp AS userStamp, (u.disabled_at IS NOT NULL) AS userDisabled
            FROM oauth_authorization a
            JOIN app_user u ON u.id = a.user_id
            JOIN oauth_client c ON c.client_id = a.client_id AND c.archived_at IS NULL
            WHERE a.id = @authorizationId AND a.revoked_at IS NULL
            """,
            new { authorizationId }, transaction, cancellationToken: cancellationToken));
        if (authorization is null || authorization.UserDisabled || !StoreSecrets.Same(authorization.UserStamp, authorization.AuthorizationStamp))
        {
            throw ApiException.NotFound("No such authorization.");
        }
        var scopes = (string[])authorization.Scopes;
        var pair = await MintAsync(connection, transaction, authorizationId, scopes, resource, Guid.NewGuid(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return pair;
    }

    public async Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId, string resource, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }
        var hash = StoreSecrets.Hash(refreshToken);
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var token = await connection.QuerySingleOrDefaultAsync<Refresh>(new CommandDefinition(
            """
            SELECT t.authorization_id AS authorizationId, t.family_id AS familyId, t.resource, t.expires_at AS expiresAt,
                   t.rotated_at AS rotatedAt, t.revoked_at AS revokedAt, a.client_id AS clientId,
                   a.revoked_at AS authorizationRevokedAt, a.security_stamp AS authorizationStamp,
                   u.security_stamp AS userStamp, (u.disabled_at IS NOT NULL) AS userDisabled, a.scopes
            FROM oauth_token t
            JOIN oauth_authorization a ON a.id = t.authorization_id
            JOIN oauth_client c ON c.client_id = a.client_id AND c.archived_at IS NULL
            JOIN app_user u ON u.id = a.user_id
            WHERE t.token_hash = @hash AND t.kind = 'refresh'
            FOR UPDATE OF t
            """,
            new { hash }, transaction, cancellationToken: cancellationToken));
        if (token is null)
        {
            return null;
        }
        if (token.RotatedAt is not null)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE oauth_token SET revoked_at = coalesce(revoked_at, @now) WHERE family_id = @familyId",
                new { now, familyId = token.FamilyId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (token.RevokedAt is not null || token.ExpiresAt <= now || !StoreSecrets.Same(resource, token.Resource)
            || token.AuthorizationRevokedAt is not null || !StoreSecrets.Same(clientId, token.ClientId)
            || token.UserDisabled || !StoreSecrets.Same(token.UserStamp, token.AuthorizationStamp))
        {
            return null;
        }
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE oauth_token SET rotated_at = @now WHERE token_hash = @hash", new { now, hash }, transaction, cancellationToken: cancellationToken));
        var pair = await MintAsync(connection, transaction, token.AuthorizationId, (string[])token.Scopes, resource, token.FamilyId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return pair;
    }

    public async Task RevokeAsync(string token, string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token))
        {
            return;
        }
        var now = clock.GetUtcNow();
        var hash = StoreSecrets.Hash(token);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var found = await connection.QuerySingleOrDefaultAsync<Spent>(new CommandDefinition(
            """
            SELECT t.authorization_id AS authorizationId, t.family_id AS familyId, t.kind, a.client_id AS clientId
            FROM oauth_token t JOIN oauth_authorization a ON a.id = t.authorization_id
            WHERE t.token_hash = @hash
            """,
            new { hash }, cancellationToken: cancellationToken));
        if (found is null || !StoreSecrets.Same(clientId, found.ClientId))
        {
            return;
        }
        await connection.ExecuteAsync(new CommandDefinition(
            found.Kind == "refresh"
                ? "UPDATE oauth_token SET revoked_at = coalesce(revoked_at, @now) WHERE family_id = @familyId"
                : "UPDATE oauth_token SET revoked_at = coalesce(revoked_at, @now) WHERE token_hash = @hash",
            new { now, familyId = found.FamilyId, hash }, cancellationToken: cancellationToken));
    }

    private async Task<AgentTokenPair> MintAsync(
        DbConnection connection, DbTransaction transaction, Guid authorizationId, string[] scopes, string resource, Guid family, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var access = StoreSecrets.New();
        var refresh = StoreSecrets.New();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO oauth_token (token_hash, kind, authorization_id, family_id, resource, expires_at)
            VALUES (@accessHash, 'access', @authorizationId, @family, @resource, @accessExpires),
                   (@refreshHash, 'refresh', @authorizationId, @family, @resource, @refreshExpires)
            """,
            new
            {
                accessHash = StoreSecrets.Hash(access),
                refreshHash = StoreSecrets.Hash(refresh),
                authorizationId,
                family,
                resource,
                accessExpires = now + AccessLifetime,
                refreshExpires = now + RefreshLifetime,
            },
            transaction, cancellationToken: cancellationToken));
        return new AgentTokenPair(access, refresh, now + AccessLifetime, new HashSet<string>(scopes, StringComparer.Ordinal));
    }
}
