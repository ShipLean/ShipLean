using System.Security.Cryptography;
using ShipLean.Application.Features.Agents.Stores;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Fakes.Agents;

internal static class AgentSecrets
{
    // The primitives live in StoreSecrets (the Postgres stores); the fakes use the same ones.
    public static string NewSecret() => StoreSecrets.New();

    public static byte[] Hash(string secret) => StoreSecrets.Hash(secret);

    public static string Base64Url(byte[] bytes) => StoreSecrets.Base64Url(bytes);

    // Call inside the lock. Compares every row in constant time.
    public static T? Find<T>(List<T> rows, Func<T, byte[]> hashOf, string secret) where T : class
    {
        var hash = Hash(secret);
        return rows.FirstOrDefault(row => CryptographicOperations.FixedTimeEquals(hashOf(row), hash));
    }

    // Kept so existing callers compile; the rule lives on OAuthClient.Verified.
    public static bool IsVerified(OAuthClient client) => client.Verified;

    public static IReadOnlyList<string> ScopeList(InMemoryAgentStore.Authorization authorization)
        => authorization.ScopeText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    public static string ScopeText(IReadOnlyCollection<string> scopes) => string.Join(' ', scopes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    // Call inside the lock. Revoking an authorization kills its tokens too.
    public static void Revoke(this InMemoryAgentStore store, InMemoryAgentStore.Authorization authorization, DateTimeOffset now)
    {
        authorization.RevokedAt ??= now;
        foreach (var token in store.Tokens.Where(t => t.AuthorizationId == authorization.Id))
        {
            token.RevokedAt ??= now;
        }
    }
}
