using System.Web;
using Shouldly;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using static ShipLean.TestSupport.Contracts.Agents.AgentWorld;

namespace ShipLean.TestSupport.Contracts.Agents;

// Behaviour every IAgentConsent has: describing a pending request to the person it belongs to, and spending it on a decision.
[TestFixture]
public abstract class AgentConsentContract
{
    public const string Issuer = "https://ship.example.test";

    protected abstract AgentWorld World { get; }

    // The consent as it answers one user's calls.
    protected abstract IAgentConsent ConsentFor(Guid userId);

    // Puts a pending request in place for the user, as /oauth/authorize would, with AgentWorld.Challenge, AgentWorld.Resource
    // and Issuer; returns its id.
    // The hook takes a user, so it cannot arrange a request bound to nobody; that case (not_found, like another user's) is
    // stated on IAgentConsent and covered by the unknown-request test.
    protected abstract Guid ArrangeRequest(Guid userId, string clientId, string redirectUri, string? state, IReadOnlyList<string> scopes);

    private async Task<(Guid UserId, string ClientId, Guid RequestId)> PendingAsync(string? state = "st-1", params string[] scopes)
    {
        var userId = await World.ArrangeUserAsync();
        var clientId = await World.RegisterClientAsync();
        var requestId = ArrangeRequest(userId, clientId, LoopbackRedirect, state, scopes.Length == 0 ? ["shiplean:read", "shiplean:write"] : scopes);
        return (userId, clientId, requestId);
    }

    private static RedirectQuery Query(string redirectUrl) => new(redirectUrl);

    private sealed class RedirectQuery
    {
        private readonly System.Collections.Specialized.NameValueCollection _values;

        public RedirectQuery(string url)
        {
            Base = url[..url.IndexOf('?', StringComparison.Ordinal)];
            _values = HttpUtility.ParseQueryString(new Uri(url).Query);
        }

        public string Base { get; }

        public string? this[string key] => _values[key];

        public IEnumerable<string?> Keys => _values.AllKeys;
    }

    [Test]
    public async Task Get_WhenPending_ShouldDescribeTheClient()
    {
        var (userId, _, requestId) = await PendingAsync();

        var request = await ConsentFor(userId).GetAsync(requestId);

        request.RequestId.ShouldBe(requestId);
        request.ClientName.ShouldBe("Contract Agent");
        request.ClientHost.ShouldBeNull();
        request.Verified.ShouldBeFalse();
        request.RedirectHost.ShouldBe("127.0.0.1");
        request.RequestedScopes.ShouldBe(["shiplean:read", "shiplean:write"]);
        request.LocalhostOnly.ShouldBeTrue();
    }

    [Test]
    public async Task Get_WhenClientHasAnInternetRedirect_ShouldNotBeLocalhostOnly()
    {
        var userId = await World.ArrangeUserAsync();
        var client = await World.Clients.RegisterAsync(new OAuthClientRegistration("Web Agent", ["https://agent.example.test/callback", LoopbackRedirect]));
        var requestId = ArrangeRequest(userId, client.ClientId, "https://agent.example.test/callback", null, ["shiplean:read"]);

        var request = await ConsentFor(userId).GetAsync(requestId);

        request.LocalhostOnly.ShouldBeFalse();
        request.RedirectHost.ShouldBe("agent.example.test");
    }

    [Test]
    public async Task Consent_WhenUnknownRequest_ShouldBeNotFound()
    {
        var userId = await World.ArrangeUserAsync();

        (await Should.ThrowAsync<ApiException>(() => ConsentFor(userId).GetAsync(Guid.NewGuid()))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => ConsentFor(userId).DecideAsync(Guid.NewGuid(), new ConsentDecision(true, []))))
            .Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Consent_WhenRequestBelongsToAnotherUser_ShouldBeNotFound()
    {
        var (_, _, requestId) = await PendingAsync();
        var other = await World.ArrangeUserAsync();

        var unknown = await Should.ThrowAsync<ApiException>(() => ConsentFor(other).GetAsync(Guid.NewGuid()));
        var foreign = await Should.ThrowAsync<ApiException>(() => ConsentFor(other).GetAsync(requestId));
        var decided = await Should.ThrowAsync<ApiException>(() => ConsentFor(other).DecideAsync(requestId, new ConsentDecision(true, [])));

        foreign.Code.ShouldBe(ApiErrorCodes.NotFound);
        foreign.Message.ShouldBe(unknown.Message);
        decided.Code.ShouldBe(ApiErrorCodes.NotFound);
        (await World.Authorizations.ListAsync(other)).ShouldBeEmpty();
    }

    [Test]
    public async Task Consent_WhenExpired_ShouldBeNotFound()
    {
        var (userId, _, requestId) = await PendingAsync();
        World.Advance(TimeSpan.FromHours(1));

        (await Should.ThrowAsync<ApiException>(() => ConsentFor(userId).GetAsync(requestId))).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Consent_WhenAllowed_ShouldRedirectWithCodeStateAndIss()
    {
        var (userId, clientId, requestId) = await PendingAsync("st-1");

        var result = await ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:read", "shiplean:write"]));

        var redirect = Query(result.RedirectUrl);
        redirect.Base.ShouldBe(LoopbackRedirect);
        redirect["state"].ShouldBe("st-1");
        redirect["iss"].ShouldBe(Issuer);
        redirect["error"].ShouldBeNull();
        var authorizationId = await World.Codes.RedeemAsync(redirect["code"]!, Verifier, LoopbackRedirect, clientId, Resource);
        var agent = (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem();
        authorizationId.ShouldBe(agent.Id);
        agent.Scopes.ShouldBe(["shiplean:read", "shiplean:write"]);
    }

    [Test]
    public async Task Consent_WhenAllowedWithoutState_ShouldLeaveStateOut()
    {
        var (userId, _, requestId) = await PendingAsync(state: null);

        var result = await ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:read"]));

        var redirect = Query(result.RedirectUrl);
        redirect.Keys.ShouldNotContain("state");
        redirect["code"].ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task Consent_WhenScopesUnticked_ShouldGrantOnlyTickedAndRead()
    {
        var (userId, _, requestId) = await PendingAsync("st-1", "shiplean:read", "shiplean:write", "shiplean:plan");

        await ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:plan"]));

        (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem().Scopes.ShouldBe(["shiplean:plan", "shiplean:read"]);
    }

    [Test]
    public async Task Consent_WhenDenied_ShouldRedirectWithAccessDenied()
    {
        var (userId, _, requestId) = await PendingAsync("st-1");

        var result = await ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(false, []));

        var redirect = Query(result.RedirectUrl);
        redirect.Base.ShouldBe(LoopbackRedirect);
        redirect["error"].ShouldBe("access_denied");
        redirect["state"].ShouldBe("st-1");
        redirect["iss"].ShouldBe(Issuer);
        redirect["code"].ShouldBeNull();
        (await World.Authorizations.ListAsync(userId)).ShouldBeEmpty();
    }

    [Test]
    public async Task Consent_WhenScopeNotRequested_ShouldBeValidation()
    {
        var (userId, _, requestId) = await PendingAsync("st-1", "shiplean:read");

        (await Should.ThrowAsync<ApiException>(() => ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:plan"]))))
            .Code.ShouldBe(ApiErrorCodes.Validation);

        (await World.Authorizations.ListAsync(userId)).ShouldBeEmpty();
        (await ConsentFor(userId).GetAsync(requestId)).RequestId.ShouldBe(requestId);
    }

    [Test]
    public async Task Consent_WhenDecidedTwice_ShouldBeNotFound()
    {
        var (userId, _, requestId) = await PendingAsync();
        await ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:read"]));

        (await Should.ThrowAsync<ApiException>(() => ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:read"]))))
            .Code.ShouldBe(ApiErrorCodes.NotFound);
        (await Should.ThrowAsync<ApiException>(() => ConsentFor(userId).GetAsync(requestId))).Code.ShouldBe(ApiErrorCodes.NotFound);
        (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Get_WhenReadNotRequested_ShouldStillListIt()
    {
        var (userId, _, requestId) = await PendingAsync("st-1", "shiplean:write");

        (await ConsentFor(userId).GetAsync(requestId)).RequestedScopes.ShouldBe(["shiplean:read", "shiplean:write"], ignoreOrder: true);
    }

    [Test]
    public async Task Decide_WhenReadTickedButNotRequested_ShouldGrantIt()
    {
        var (userId, _, requestId) = await PendingAsync("st-1", "shiplean:write");

        await ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:read", "shiplean:write"]));

        (await World.Authorizations.ListAsync(userId)).ShouldHaveSingleItem().Scopes.ShouldBe(["shiplean:read", "shiplean:write"]);
    }

    [Test]
    public async Task Consent_WhenRedirectUriHasAQuery_ShouldAppendWithAmpersand()
    {
        var userId = await World.ArrangeUserAsync();
        const string redirectUri = LoopbackRedirect + "?a=1";
        var client = await World.Clients.RegisterAsync(new OAuthClientRegistration("Query Agent", [redirectUri]));
        var requestId = ArrangeRequest(userId, client.ClientId, redirectUri, "st-1", ["shiplean:read"]);

        var result = await ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:read"]));

        result.RedirectUrl.ShouldStartWith(redirectUri + "&code=");
        var redirect = Query(result.RedirectUrl);
        redirect["a"].ShouldBe("1");
        redirect["state"].ShouldBe("st-1");
        redirect["iss"].ShouldBe(Issuer);
    }

    [Test]
    public async Task Consent_WhenStateNeedsEscaping_ShouldRoundTripIt()
    {
        var (userId, _, requestId) = await PendingAsync("a b&c=d");

        var result = await ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(false, []));

        var redirect = Query(result.RedirectUrl);
        redirect["state"].ShouldBe("a b&c=d");
        redirect["iss"].ShouldBe(Issuer);
        redirect.Keys.ShouldNotContain("c");
    }

    [Test]
    public async Task Decide_WhenExpired_ShouldBeNotFound()
    {
        var (userId, _, requestId) = await PendingAsync();
        World.Advance(TimeSpan.FromHours(1));

        (await Should.ThrowAsync<ApiException>(() => ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:read"]))))
            .Code.ShouldBe(ApiErrorCodes.NotFound);
        (await World.Authorizations.ListAsync(userId)).ShouldBeEmpty();
    }

    [Test]
    public async Task Consent_WhenDeniedTwice_ShouldBeNotFound()
    {
        var (userId, _, requestId) = await PendingAsync();
        await ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(false, []));

        (await Should.ThrowAsync<ApiException>(() => ConsentFor(userId).DecideAsync(requestId, new ConsentDecision(true, ["shiplean:read"]))))
            .Code.ShouldBe(ApiErrorCodes.NotFound);
        (await World.Authorizations.ListAsync(userId)).ShouldBeEmpty();
    }
}
