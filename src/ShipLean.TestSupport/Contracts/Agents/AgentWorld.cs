using ShipLean.Contracts.Agents;

namespace ShipLean.TestSupport.Contracts.Agents;

// What the five agent contract fixtures share, as a class of its own (a fixture may not inherit another): the five services under test (the in-memory fakes now, the Postgres
// ones in wave 1) and the hooks a derivation provides to arrange users and move time. Every fixture works through the
// public interfaces only, so a derivation on the real stores passes the same tests.
public abstract class AgentWorld
{
    public const string Resource = "https://ship.example.test/mcp";
    public const string LoopbackRedirect = "http://127.0.0.1:8123/callback";

    // A valid S256 pair (RFC 7636 appendix B).
    public const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    public const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";

    public abstract IAgentTokens Tokens { get; }

    public abstract IAgentTokenIssuer Issuer { get; }

    public abstract IOAuthClients Clients { get; }

    public abstract IOAuthCodes Codes { get; }

    public abstract IAgentAuthorizations Authorizations { get; }

    public abstract IOAuthRequests Requests { get; }

    // The consent service answering as the given user.
    public abstract IAgentConsent ConsentFor(Guid userId);

    // The clock the services read.
    public abstract DateTimeOffset Now { get; }

    public abstract void Advance(TimeSpan by);

    // An active user.
    public abstract Task<Guid> ArrangeUserAsync();

    // Disables the user, as an admin would.
    public abstract Task DisableUserAsync(Guid userId);

    // Archives the client, as an operator would: it is then unknown to IOAuthClients.FindAsync and its authorizations stop working.
    public abstract Task ArchiveClientAsync(string clientId);

    // Rotates the user's security stamp, as a password change would.
    public abstract Task RotateSecurityStampAsync(Guid userId);

    public async Task<string> RegisterClientAsync(string name = "Contract Agent")
        => (await Clients.RegisterAsync(new OAuthClientRegistration(name, [LoopbackRedirect]))).ClientId;

    private static HashSet<string> Scopes(params string[] scopes) => new HashSet<string>(scopes, StringComparer.Ordinal);

    // A user who consented to a freshly registered client; no tokens yet.
    public async Task<(Guid UserId, string ClientId, Guid AuthorizationId)> AuthorizeAsync(params string[] scopes)
    {
        var userId = await ArrangeUserAsync();
        var clientId = await RegisterClientAsync();
        var authorizationId = await Authorizations.CreateAsync(userId, clientId, Scopes(scopes.Length == 0 ? ["shiplean:read"] : scopes));
        return (userId, clientId, authorizationId);
    }

    public async Task<(Guid UserId, Guid AuthorizationId, AgentTokenPair Pair)> IssueAsync(params string[] scopes)
    {
        var (userId, clientId, authorizationId) = await AuthorizeAsync(scopes);
        var pair = await Issuer.IssueAsync(authorizationId, Resource);
        _clientOfRefresh[pair.RefreshToken] = clientId;
        _clientOfRefresh[pair.AccessToken] = clientId;
        return (userId, authorizationId, pair);
    }

    // Refreshes as the client the refresh token was issued to (for a token from IssueAsync or an earlier RefreshAsync here).
    public async Task<AgentTokenPair?> RefreshAsync(string refreshToken, string resource = Resource)
    {
        var pair = await Issuer.RefreshAsync(refreshToken, _clientOfRefresh.GetValueOrDefault(refreshToken, "unknown-client"), resource);
        if (pair is not null)
        {
            _clientOfRefresh[pair.RefreshToken] = _clientOfRefresh.GetValueOrDefault(refreshToken, "unknown-client");
            _clientOfRefresh[pair.AccessToken] = _clientOfRefresh.GetValueOrDefault(refreshToken, "unknown-client");
        }
        return pair;
    }

    // Revokes as the client the token was issued to.
    public Task RevokeAsync(string token)
        => Issuer.RevokeAsync(token, _clientOfRefresh.GetValueOrDefault(token, "unknown-client"));

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _clientOfRefresh = new(StringComparer.Ordinal);
}
