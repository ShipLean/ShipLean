using ShipLean.Application.Features.Agents.Stores;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Fakes.Agents;

public sealed class InMemoryOAuthClients(InMemoryAgentStore store) : IOAuthClients
{
    public Task<OAuthClient?> FindAsync(string clientId, CancellationToken cancellationToken = default)
    {
        lock (store.Gate)
        {
            return Task.FromResult(store.Clients.GetValueOrDefault(clientId));
        }
    }

    public Task<OAuthClient> RegisterAsync(OAuthClientRegistration request, CancellationToken cancellationToken = default)
    {
        var (name, redirectUris) = ClientRegistrationRules.Check(request);

        var client = new OAuthClient(AgentSecrets.NewSecret(), OAuthClientKind.Registered, name, null, redirectUris);
        lock (store.Gate)
        {
            store.Clients[client.ClientId] = client;
        }
        return Task.FromResult(client);
    }
}
