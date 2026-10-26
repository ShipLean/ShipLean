using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Fakes.Agents;

public sealed class InMemoryAgentTokens(InMemoryAgentStore store, IUserDirectory users, IUserAccounts accounts, TimeProvider clock) : IAgentTokens
{
    private static readonly TimeSpan LastUsedGranularity = TimeSpan.FromMinutes(1);

    public async Task<AgentPrincipal?> ValidateAsync(string bearer, string resource, CancellationToken cancellationToken = default)
    {
        Guid userId;
        lock (store.Gate)
        {
            var found = AgentSecrets.Find(store.Tokens, t => t.SecretHash, bearer ?? string.Empty);
            if (found is null)
            {
                return null;
            }
            userId = store.Authorizations.First(a => a.Id == found.AuthorizationId).UserId;
        }
        var currentStamp = await AgentUserState.CurrentStampAsync(users, accounts, userId, cancellationToken);

        lock (store.Gate)
        {
            var token = AgentSecrets.Find(store.Tokens, t => t.SecretHash, bearer ?? string.Empty);
            if (token is not { IsRefresh: false })
            {
                return null;
            }
            var now = clock.GetUtcNow();
            var authorization = store.Authorizations.First(a => a.Id == token.AuthorizationId);
            if (token.RevokedAt is not null || token.ExpiresAt <= now || token.Resource != resource
                || authorization.RevokedAt is not null || currentStamp is null || currentStamp != authorization.SecurityStamp)
            {
                return null;
            }
            if (authorization.LastUsedAt is not { } last || now - last >= LastUsedGranularity)
            {
                authorization.LastUsedAt = now;
            }
            var client = store.Clients[authorization.ClientId];
            return new AgentPrincipal(
                authorization.UserId, authorization.Id, client.ClientId, client.ClientName, client.ClientHost,
                client.Verified, new HashSet<string>(AgentSecrets.ScopeList(authorization), StringComparer.Ordinal));
        }
    }

}
