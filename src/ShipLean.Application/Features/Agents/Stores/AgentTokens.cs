using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Stores;

// Every /mcp call lands here: one read joins the token, its authorization, the client and the user, so a revoked token, a
// disabled user and a rotated security stamp all end the token on its next use. last_used_at is written at most once a minute.
public sealed class AgentTokens(NpgsqlDataSource dataSource, TimeProvider clock) : IAgentTokens
{
    private static readonly TimeSpan LastUsedGranularity = TimeSpan.FromMinutes(1);

    private sealed record Row(
        byte[] TokenHash,
        string Kind,
        string Resource,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RevokedAt,
        Guid AuthorizationId,
        Guid UserId,
        Array Scopes,
        string AuthorizationStamp,
        DateTimeOffset? AuthorizationRevokedAt,
        DateTimeOffset? LastUsedAt,
        string ClientId,
        string ClientName,
        string? ClientHost,
        string ClientKind,
        string UserStamp,
        bool UserDisabled)
    {
    }

    public async Task<AgentPrincipal?> ValidateAsync(string bearer, string resource, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(bearer))
        {
            return null;
        }
        var hash = StoreSecrets.Hash(bearer);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            SELECT t.token_hash AS tokenHash, t.kind, t.resource, t.expires_at AS expiresAt, t.revoked_at AS revokedAt,
                   a.id AS authorizationId, a.user_id AS userId, a.scopes, a.security_stamp AS authorizationStamp,
                   a.revoked_at AS authorizationRevokedAt, a.last_used_at AS lastUsedAt,
                   c.client_id AS clientId, c.client_name AS clientName, c.client_host AS clientHost, c.kind AS clientKind,
                   u.security_stamp AS userStamp, (u.disabled_at IS NOT NULL) AS userDisabled
            FROM oauth_token t
            JOIN oauth_authorization a ON a.id = t.authorization_id
            JOIN oauth_client c ON c.client_id = a.client_id AND c.archived_at IS NULL
            JOIN app_user u ON u.id = a.user_id
            WHERE t.token_hash = @hash
            """,
            new { hash }, cancellationToken: cancellationToken));
        var now = clock.GetUtcNow();
        if (row is null || !StoreSecrets.Same(row.TokenHash, hash) || row.Kind != "access" || row.RevokedAt is not null
            || row.ExpiresAt <= now || !StoreSecrets.Same(resource, row.Resource)
            || row.AuthorizationRevokedAt is not null || row.UserDisabled || !StoreSecrets.Same(row.UserStamp, row.AuthorizationStamp))
        {
            return null;
        }
        if (row.LastUsedAt is not { } last || now - last >= LastUsedGranularity)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE oauth_authorization SET last_used_at = @now WHERE id = @id AND (last_used_at IS NULL OR last_used_at < @now)",
                new { now, id = row.AuthorizationId }, cancellationToken: cancellationToken));
        }
        return new AgentPrincipal(
            row.UserId, row.AuthorizationId, row.ClientId, row.ClientName, row.ClientHost,
            row.ClientKind == "metadataDocument" && row.ClientHost is not null, new HashSet<string>((string[])row.Scopes, StringComparer.Ordinal));
    }
}
