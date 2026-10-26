using System.Net;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Mcp;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// SHIP-63 J1: with the token header set to Authorization, the proxy's Bearer JWT signs in on /api, and an agent's own
// bearer token on /mcp is answered as before.
[TestFixture]
public class ProxyBearerJourneyTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override bool UseIdentityProvider => true;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => Provider!.Settings(tokenHeader: "Authorization");

    [Test]
    public async Task TokenSignIn_WhenAuthorizationHeaderMode_ShouldNotDisturbAgents()
    {
        var email = $"bearer-{Guid.NewGuid():N}@shiplean.test";
        using var browser = ProxyClient().WithToken("Authorization", Provider!.Mint(email, "Bea Rer"));
        using var signedIn = await browser.GetAsync("/api/me");
        signedIn.StatusCode.ShouldBe(HttpStatusCode.OK, await signedIn.Content.ReadAsStringAsync());
        (await signedIn.Content.ReadAsStringAsync()).ShouldContain(email);

        var agentToken = await Host.IssueAgentToken(TestUser.Mei);
        await using var mcp = await McpTestClient.ConnectAsync(Host, agentToken);
        var tools = await mcp.ListToolsAsync();

        tools.ShouldNotBeEmpty();
    }

    // The proxy's JWT is not an agent token: it must never authenticate /mcp.
    [Test]
    public async Task Mcp_WhenBearerIsProxyToken_ShouldAnswerUnauthorized()
    {
        using var http = new HttpClient { BaseAddress = new Uri(Host.BaseUrl) };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Provider!.Mint("proxy-on-mcp@shiplean.test", "Nope"));

        using var response = await http.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
