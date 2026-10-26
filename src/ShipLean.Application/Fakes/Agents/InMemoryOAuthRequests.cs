using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;

namespace ShipLean.Application.Fakes.Agents;

// Puts the request where InMemoryAgentConsent reads it, so IAgentConsent.GetAsync describes what was created.
public sealed class InMemoryOAuthRequests(
    InMemoryAgentStore store,
    InMemoryAgentConsent consent,
    IUserDirectory users,
    IAgentAddresses addresses) : IOAuthRequests
{
    public async Task<Guid> CreateAsync(OAuthRequestCreate request, CancellationToken cancellationToken = default)
    {
        bool clientKnown;
        lock (store.Gate)
        {
            clientKnown = store.Clients.ContainsKey(request.ClientId);
        }
        if (!clientKnown)
        {
            throw ApiException.NotFound("No such client.");
        }
        await users.GetAsync(request.UserId, cancellationToken);

        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        return consent.Add(request.UserId, request.ClientId, request.RedirectUri, request.State, request.CodeChallenge, request.Scopes, request.Resource, issuer);
    }
}
