using ShipLean.Contracts.Agents;
using ShipLean.Core;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Fakes.Agents;

// The stamp comes from IUserAccounts and the user's existence from IUserDirectory, so a user changed elsewhere is seen.
public sealed class InMemoryAgentAuthorizations(InMemoryAgentStore store, IUserDirectory users, IUserAccounts accounts, TimeProvider clock) : IAgentAuthorizations
{
    public async Task<Guid> CreateAsync(Guid userId, string clientId, IReadOnlySet<string> scopes, CancellationToken cancellationToken = default)
    {
        await users.GetAsync(userId, cancellationToken);
        var stamp = await accounts.GetSecurityStampAsync(userId, cancellationToken);
        lock (store.Gate)
        {
            if (!store.Clients.ContainsKey(clientId))
            {
                throw ApiException.NotFound("No such OAuth client.");
            }
            var live = store.Authorizations.FirstOrDefault(a => a.UserId == userId && a.ClientId == clientId && a.RevokedAt is null);
            if (live is not null && live.SecurityStamp != stamp)
            {
                // Granted under an earlier password: end it and its tokens rather than revive them.
                store.Revoke(live, clock.GetUtcNow());
            }
            else if (live is not null)
            {
                live.ScopeText = AgentSecrets.ScopeText(AgentSecrets.ScopeList(live).Concat(scopes).ToArray());
                return live.Id;
            }
            var authorization = new InMemoryAgentStore.Authorization
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ClientId = clientId,
                ScopeText = AgentSecrets.ScopeText(scopes),
                SecurityStamp = stamp,
                CreatedAt = clock.GetUtcNow(),
            };
            store.Authorizations.Add(authorization);
            return authorization.Id;
        }
    }

    public Task<IReadOnlyList<ConnectedAgentDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        lock (store.Gate)
        {
            IReadOnlyList<ConnectedAgentDto> list = store.Authorizations
                .Where(a => a.UserId == userId && a.RevokedAt is null)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a =>
                {
                    var client = store.Clients[a.ClientId];
                    return new ConnectedAgentDto(
                        a.Id, client.ClientName, client.ClientHost, client.Verified,
                        AgentSecrets.ScopeList(a), a.CreatedAt, a.LastUsedAt,
                        DisplayDate.Relative(a.CreatedAt, clock), a.LastUsedAt is { } used ? DisplayDate.Relative(used, clock) : null);
                })
                .ToList();
            return Task.FromResult(list);
        }
    }

    public Task RevokeAsync(Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        lock (store.Gate)
        {
            var authorization = store.Authorizations.FirstOrDefault(a => a.Id == id && a.UserId == userId)
                                ?? throw ApiException.NotFound("No such connected agent.");
            store.Revoke(authorization, clock.GetUtcNow());
        }
        return Task.CompletedTask;
    }
}
