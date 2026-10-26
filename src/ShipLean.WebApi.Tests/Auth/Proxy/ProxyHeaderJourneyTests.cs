using System.Net;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting.Proxy;

namespace ShipLean.WebApi.Tests.Auth.Proxy;

// SHIP-63 J1: plain-header mode with the real reader, from the loopback peer the proxy would be.
[TestFixture]
public class ProxyHeaderJourneyTests : ProxyApiFixture
{
    protected override bool UseFakeReader => false;

    protected override IReadOnlyDictionary<string, string?> ProxySettings => TestIdentityProvider.HeaderSettings();

    [Test]
    public async Task HeaderSignIn_WhenFromLoopback_ShouldSignIn()
    {
        var email = $"header-{Guid.NewGuid():N}@shiplean.test";
        using var client = ProxyClient().WithHeaders(("Remote-Email", email), ("Remote-Name", "Hedda Header"));

        using var response = await client.GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).ShouldContain(email);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Hedda Header");
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("shiplean.auth=", StringComparison.Ordinal));
    }
}
