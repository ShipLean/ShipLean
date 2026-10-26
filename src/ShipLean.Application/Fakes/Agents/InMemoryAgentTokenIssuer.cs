using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Fakes.Agents;

public sealed class InMemoryAgentTokenIssuer(InMemoryAgentStore store, IUserDirectory users, IUserAccounts accounts, TimeProvider clock) : IAgentTokenIssuer
{
    public static readonly TimeSpan AccessLifetime = TimeSpan.FromHours(1);
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(90);

    public async Task<AgentTokenPair> IssueAsync(Guid authorizationId, string resource, CancellationToken cancellationToken = default)
    {
        Guid userId;
        lock (store.Gate)
        {
            userId = (store.Authorizations.FirstOrDefault(a => a.Id == authorizationId && a.RevokedAt is null)
                      ?? throw ApiException.NotFound("No such authorization.")).UserId;
        }
        var currentStamp = await AgentUserState.CurrentStampAsync(users, accounts, userId, cancellationToken);

        lock (store.Gate)
        {
            var authorization = store.Authorizations.FirstOrDefault(a => a.Id == authorizationId && a.RevokedAt is null)
                                ?? throw ApiException.NotFound("No such authorization.");
            return currentStamp is null || currentStamp != authorization.SecurityStamp || !store.Clients.ContainsKey(authorization.ClientId)
                ? throw ApiException.NotFound("No such authorization.")
                : Mint(authorization, resource, Guid.NewGuid());
        }
    }

    public async Task<AgentTokenPair?> RefreshAsync(string refreshToken, string clientId, string resource, CancellationToken cancellationToken = default)
    {
        Guid userId;
        lock (store.Gate)
        {
            var found = AgentSecrets.Find(store.Tokens, t => t.SecretHash, refreshToken ?? string.Empty);
            if (found is not { IsRefresh: true })
            {
                return null;
            }
            userId = store.Authorizations.First(a => a.Id == found.AuthorizationId).UserId;
        }
        var currentStamp = await AgentUserState.CurrentStampAsync(users, accounts, userId, cancellationToken);

        lock (store.Gate)
        {
            var token = AgentSecrets.Find(store.Tokens, t => t.SecretHash, refreshToken ?? string.Empty);
            if (token is not { IsRefresh: true })
            {
                return null;
            }
            var now = clock.GetUtcNow();
            if (token.RotatedAt is not null)
            {
                foreach (var sibling in store.Tokens.Where(t => t.FamilyId == token.FamilyId))
                {
                    sibling.RevokedAt ??= now;
                }
                return null;
            }
            var authorization = store.Authorizations.First(a => a.Id == token.AuthorizationId);
            if (token.RevokedAt is not null || token.ExpiresAt <= now || token.Resource != resource
                || authorization.RevokedAt is not null || authorization.ClientId != clientId || currentStamp is null || currentStamp != authorization.SecurityStamp)
            {
                return null;
            }
            token.RotatedAt = now;
            return Mint(authorization, resource, token.FamilyId);
        }
    }

    public Task RevokeAsync(string token, string clientId, CancellationToken cancellationToken = default)
    {
        lock (store.Gate)
        {
            var found = AgentSecrets.Find(store.Tokens, t => t.SecretHash, token ?? string.Empty);
            if (found is null || store.Authorizations.First(a => a.Id == found.AuthorizationId).ClientId != clientId)
            {
                return Task.CompletedTask;
            }
            var now = clock.GetUtcNow();
            foreach (var row in found.IsRefresh ? store.Tokens.Where(t => t.FamilyId == found.FamilyId) : new[] { found })
            {
                row.RevokedAt ??= now;
            }
        }
        return Task.CompletedTask;
    }

    // Call inside the lock.
    private AgentTokenPair Mint(InMemoryAgentStore.Authorization authorization, string resource, Guid family)
    {
        var now = clock.GetUtcNow();
        var access = AgentSecrets.NewSecret();
        var refresh = AgentSecrets.NewSecret();
        store.Tokens.Add(New(access, isRefresh: false, authorization, resource, family, now + AccessLifetime));
        store.Tokens.Add(New(refresh, isRefresh: true, authorization, resource, family, now + RefreshLifetime));
        return new AgentTokenPair(access, refresh, now + AccessLifetime, new HashSet<string>(AgentSecrets.ScopeList(authorization), StringComparer.Ordinal));
    }

    private static InMemoryAgentStore.Token New(string secret, bool isRefresh, InMemoryAgentStore.Authorization authorization, string resource, Guid family, DateTimeOffset expiresAt)
        => new()
        {
            SecretHash = AgentSecrets.Hash(secret),
            IsRefresh = isRefresh,
            AuthorizationId = authorization.Id,
            FamilyId = family,
            Resource = resource,
            ExpiresAt = expiresAt,
        };

}
