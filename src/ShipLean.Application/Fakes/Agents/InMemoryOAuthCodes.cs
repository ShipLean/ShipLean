using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Fakes.Agents;

public sealed partial class InMemoryOAuthCodes(InMemoryAgentStore store, TimeProvider clock) : IOAuthCodes
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    // BASE64URL(SHA256(verifier)) is 43 characters.
    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex ChallengeShape();

    // RFC 7636: 43 to 128 unreserved characters.
    [GeneratedRegex("^[A-Za-z0-9._~-]{43,128}$")]
    private static partial Regex VerifierShape();

    public Task<string> IssueAsync(Guid authorizationId, string codeChallenge, string redirectUri, string resource, CancellationToken cancellationToken = default)
    {
        if (codeChallenge is null || !ChallengeShape().IsMatch(codeChallenge))
        {
            throw ApiException.Validation("code_challenge must be an S256 challenge.");
        }
        var code = AgentSecrets.NewSecret();
        lock (store.Gate)
        {
            if (store.Authorizations.FirstOrDefault(a => a.Id == authorizationId && a.RevokedAt is null) is null)
            {
                throw ApiException.NotFound("No such authorization.");
            }
            store.Codes.Add(new InMemoryAgentStore.Code
            {
                SecretHash = AgentSecrets.Hash(code),
                AuthorizationId = authorizationId,
                CodeChallenge = codeChallenge,
                RedirectUri = redirectUri,
                Resource = resource,
                ExpiresAt = clock.GetUtcNow() + Lifetime,
            });
        }
        return Task.FromResult(code);
    }

    // Any attempt on a known, unused, unexpired code spends it, right or wrong, so a guessed verifier gets one try.
    public Task<Guid?> RedeemAsync(string code, string codeVerifier, string redirectUri, string clientId, string resource, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        lock (store.Gate)
        {
            var row = AgentSecrets.Find(store.Codes, c => c.SecretHash, code ?? string.Empty);
            if (row is null)
            {
                return Task.FromResult<Guid?>(null);
            }
            var authorization = store.Authorizations.First(a => a.Id == row.AuthorizationId);
            if (row.UsedAt is not null)
            {
                store.Revoke(authorization, now);
                return Task.FromResult<Guid?>(null);
            }
            if (row.ExpiresAt <= now)
            {
                return Task.FromResult<Guid?>(null);
            }
            row.UsedAt = now;

            var matches = authorization.RevokedAt is null
                          && VerifierShape().IsMatch(codeVerifier ?? string.Empty)
                          && Same(AgentSecrets.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier!))), row.CodeChallenge)
                          && Same(redirectUri, row.RedirectUri)
                          && Same(clientId, authorization.ClientId)
                          && Same(resource, row.Resource);
            return Task.FromResult<Guid?>(matches ? row.AuthorizationId : null);
        }
    }

    private static bool Same(string? given, string expected)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given ?? string.Empty), Encoding.UTF8.GetBytes(expected));
}
