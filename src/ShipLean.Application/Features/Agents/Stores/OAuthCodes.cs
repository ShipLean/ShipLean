using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Stores;

// oauth_code: the code is stored as its SHA-256 only. A known, unused, unexpired code is spent by any attempt on it, right
// or wrong, so a guessed verifier gets one try; the row is locked while it is judged, so a concurrent redeem waits and then
// finds it used.
public sealed partial class OAuthCodes(NpgsqlDataSource dataSource, TimeProvider clock) : IOAuthCodes
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    // BASE64URL(SHA256(verifier)) is 43 characters.
    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex ChallengeShape();

    // RFC 7636: 43 to 128 unreserved characters.
    [GeneratedRegex("^[A-Za-z0-9._~-]{43,128}$")]
    private static partial Regex VerifierShape();

    private sealed record Row(
        byte[] CodeHash,
        Guid AuthorizationId,
        string CodeChallenge,
        string RedirectUri,
        string Resource,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? UsedAt,
        string ClientId,
        DateTimeOffset? AuthorizationRevokedAt)
    {
    }

    public async Task<string> IssueAsync(Guid authorizationId, string codeChallenge, string redirectUri, string resource, CancellationToken cancellationToken = default)
    {
        if (codeChallenge is null || !ChallengeShape().IsMatch(codeChallenge))
        {
            throw ApiException.Validation("code_challenge must be an S256 challenge.");
        }
        var code = StoreSecrets.New();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO oauth_code (code_hash, authorization_id, code_challenge, redirect_uri, resource, expires_at)
            SELECT @hash, a.id, @codeChallenge, @redirectUri, @resource, @expiresAt
            FROM oauth_authorization a WHERE a.id = @authorizationId AND a.revoked_at IS NULL
            """,
            new { hash = StoreSecrets.Hash(code), authorizationId, codeChallenge, redirectUri, resource, expiresAt = clock.GetUtcNow() + Lifetime },
            cancellationToken: cancellationToken));
        return inserted == 1 ? code : throw ApiException.NotFound("No such authorization.");
    }

    public async Task<Guid?> RedeemAsync(string code, string codeVerifier, string redirectUri, string clientId, string resource, CancellationToken cancellationToken = default)
    {
        var hash = StoreSecrets.Hash(code);
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            """
            SELECT c.code_hash AS codeHash, c.authorization_id AS authorizationId, c.code_challenge AS codeChallenge,
                   c.redirect_uri AS redirectUri, c.resource, c.expires_at AS expiresAt, c.used_at AS usedAt,
                   a.client_id AS clientId, a.revoked_at AS authorizationRevokedAt
            FROM oauth_code c
            JOIN oauth_authorization a ON a.id = c.authorization_id
            WHERE c.code_hash = @hash
            FOR UPDATE OF c
            """,
            new { hash }, transaction, cancellationToken: cancellationToken));
        if (row is null || !StoreSecrets.Same(row.CodeHash, hash))
        {
            return null;
        }
        if (row.UsedAt is not null)
        {
            // RFC 6749 section 4.1.2: a code used twice ends the authorization it made.
            await AuthorizationRevocation.RevokeAsync(connection, transaction, row.AuthorizationId, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (row.ExpiresAt <= now)
        {
            return null;
        }
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE oauth_code SET used_at = @now WHERE code_hash = @hash", new { now, hash }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);

        var matches = row.AuthorizationRevokedAt is null
                      && VerifierShape().IsMatch(codeVerifier ?? string.Empty)
                      && StoreSecrets.Same(StoreSecrets.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier!))), row.CodeChallenge)
                      && StoreSecrets.Same(redirectUri, row.RedirectUri)
                      && StoreSecrets.Same(clientId, row.ClientId)
                      && StoreSecrets.Same(resource, row.Resource);
        return matches ? row.AuthorizationId : null;
    }
}
