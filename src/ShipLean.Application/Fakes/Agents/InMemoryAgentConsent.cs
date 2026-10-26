using ShipLean.Application.Features.Agents.Clients;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Fakes.Agents;

// Pending requests are held here, put in place by Add: the real ones are made by /oauth/authorize.
public sealed class InMemoryAgentConsent(
    InMemoryAgentStore store,
    IAgentAuthorizations authorizations,
    IOAuthCodes codes,
    ICurrentUser currentUser,
    TimeProvider clock) : IAgentConsent
{
    public static readonly TimeSpan Lifetime = OAuthRequestCreate.Lifetime;

    private const string Read = "shiplean:read";
    private const string NotFoundMessage = "No such authorization request.";

    private sealed record Pending(
        Guid Id, Guid UserId, string ClientId, string RedirectUri, string? State, string CodeChallenge,
        IReadOnlyList<string> Scopes, string Resource, string Issuer, DateTimeOffset ExpiresAt);

    private readonly Dictionary<Guid, Pending> _pending = [];

    // Puts a request in place for a user and returns its id. issuer is the authorization server's issuer, which comes
    // back as iss.
    public Guid Add(
        Guid userId, string clientId, string redirectUri, string? state, string codeChallenge,
        IReadOnlyList<string> scopes, string resource, string issuer)
    {
        var id = Guid.NewGuid();
        lock (store.Gate)
        {
            _pending[id] = new Pending(id, userId, clientId, redirectUri, state, codeChallenge, scopes, resource, issuer, clock.GetUtcNow() + Lifetime);
        }
        return id;
    }

    public Task<ConsentRequestDto> GetAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        lock (store.Gate)
        {
            var request = Find(requestId);
            var client = store.Clients[request.ClientId];
            var redirectHost = RedirectUriRules.Describe(new Uri(request.RedirectUri));
            return Task.FromResult(new ConsentRequestDto(
                request.Id, client.ClientName, client.ClientHost, AgentSecrets.IsVerified(client), redirectHost,
                WithRead(request.Scopes), client.RedirectUris.All(OAuthClient.IsLoopback)));
        }
    }

    public async Task<ConsentResult> DecideAsync(Guid requestId, ConsentDecision decision, CancellationToken cancellationToken = default)
    {
        Pending request;
        lock (store.Gate)
        {
            request = Find(requestId);
            if (decision.Allow && (decision.Scopes ?? []).Any(s => s != Read && !request.Scopes.Contains(s)))
            {
                throw ApiException.Validation("A ticked scope was not requested.");
            }
            _pending.Remove(requestId);
        }

        var query = new List<KeyValuePair<string, string>>();
        if (decision.Allow)
        {
            var granted = new HashSet<string>(decision.Scopes ?? [], StringComparer.Ordinal) { Read };
            var authorizationId = await authorizations.CreateAsync(request.UserId, request.ClientId, granted, cancellationToken);
            var code = await codes.IssueAsync(authorizationId, request.CodeChallenge, request.RedirectUri, request.Resource, cancellationToken);
            query.Add(new("code", code));
        }
        else
        {
            query.Add(new("error", "access_denied"));
        }
        if (request.State is not null)
        {
            query.Add(new("state", request.State));
        }
        query.Add(new("iss", request.Issuer));
        return new ConsentResult(Redirect(request.RedirectUri, query));
    }

    private static IReadOnlyList<string> WithRead(IReadOnlyList<string> scopes)
        => scopes.Contains(Read) ? scopes : [Read, .. scopes];

    // Call inside the lock. Another user's request answers like an unknown one.
    private Pending Find(Guid requestId)
        => _pending.TryGetValue(requestId, out var request) && request.UserId == currentUser.Id && request.ExpiresAt > clock.GetUtcNow()
            ? request
            : throw ApiException.NotFound(NotFoundMessage);

    private static string Redirect(string redirectUri, List<KeyValuePair<string, string>> query)
    {
        var separator = redirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return redirectUri + separator + string.Join('&', query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    }
}
