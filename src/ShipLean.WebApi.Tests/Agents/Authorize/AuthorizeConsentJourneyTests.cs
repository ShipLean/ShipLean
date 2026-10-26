using System.Net.Http.Json;
using ShipLean.Contracts.Agents;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Agents.Authorize;

// authorize -> the consent API -> the client's redirect, as a browser does it. Run over the Postgres stores and over the
// in-memory fakes: the journey must not depend on which implementation of the contracts is registered.
public abstract class AuthorizeConsentJourneyTests : AuthorizeFixtureBase
{
    [Test]
    public async Task Authorize_WhenConsented_ShouldRedirectWithCodeStateAndIss()
    {
        var clientId = await ArrangeClientAsync();
        using var browser = await SignedInBrowserAsync();

        using var authorize = await browser.GetAsync(AuthorizeUrl(clientId));
        var requestId = RequestIdOf(Location(authorize));
        var result = await DecideAsync(browser, requestId, new ConsentDecision(true, ["shiplean:read", "shiplean:write"]));

        result.RedirectUrl.ShouldStartWith(Redirect + "?");
        var query = QueryOf(new Uri(result.RedirectUrl));
        query["code"].ToString().ShouldNotBeNullOrEmpty();
        query["state"].ToString().ShouldBe("st-1");
        query["iss"].ToString().ShouldBe(Issuer);
        var agents = await browser.GetFromJsonAsync<List<ConnectedAgentDto>>("/api/me/agents", AuthClient.Json);
        agents.ShouldHaveSingleItem().Scopes.ShouldBe(["shiplean:read", "shiplean:write"], ignoreOrder: true);
    }

    [Test]
    public async Task Authorize_WhenCustomSchemeRedirectAndConsented_ShouldRedirectWithCodeStateAndIss()
    {
        const string cursor = "cursor://anysphere.cursor-retrieval/oauth/callback";
        var clientId = await ArrangeClientAsync(redirect: cursor);
        using var browser = await SignedInBrowserAsync();

        using var authorize = await browser.GetAsync(AuthorizeUrl(clientId, ("redirect_uri", cursor)));
        var requestId = RequestIdOf(Location(authorize));
        var consent = await browser.GetFromJsonAsync<ConsentRequestDto>($"/api/agents/consent/{requestId}", AuthClient.Json);
        consent!.RedirectHost.ShouldBe("cursor://anysphere.cursor-retrieval");
        consent.LocalhostOnly.ShouldBeFalse();
        var result = await DecideAsync(browser, requestId, new ConsentDecision(true, ["shiplean:read", "shiplean:write"]));

        result.RedirectUrl.ShouldStartWith(cursor + "?");
        var query = QueryOf(new Uri(result.RedirectUrl));
        query["code"].ToString().ShouldNotBeNullOrEmpty();
        query["state"].ToString().ShouldBe("st-1");
        query["iss"].ToString().ShouldBe(Issuer);
    }

    [Test]
    public async Task Authorize_WhenDenied_ShouldRedirectAccessDenied()
    {
        var clientId = await ArrangeClientAsync();
        using var browser = await SignedInBrowserAsync();

        using var authorize = await browser.GetAsync(AuthorizeUrl(clientId, ("state", "st-deny")));
        var result = await DecideAsync(browser, RequestIdOf(Location(authorize)), new ConsentDecision(false, []));

        result.RedirectUrl.ShouldStartWith(Redirect + "?");
        var query = QueryOf(new Uri(result.RedirectUrl));
        query["error"].ToString().ShouldBe("access_denied");
        query["state"].ToString().ShouldBe("st-deny");
        query["iss"].ToString().ShouldBe(Issuer);
        query.ContainsKey("code").ShouldBeFalse();
    }

    [Test]
    public async Task Authorize_WhenNoScope_ShouldAskForReadAndWrite()
    {
        var clientId = await ArrangeClientAsync();
        using var browser = await SignedInBrowserAsync();

        using var authorize = await browser.GetAsync(AuthorizeUrl(clientId, ("scope", null)));
        var requestId = RequestIdOf(Location(authorize));

        var consent = await browser.GetFromJsonAsync<ConsentRequestDto>($"/api/agents/consent/{requestId}", AuthClient.Json);
        consent!.RequestedScopes.ShouldBe(["shiplean:read", "shiplean:write"], ignoreOrder: true);
    }
}
