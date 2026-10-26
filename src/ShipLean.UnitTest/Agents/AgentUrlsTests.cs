using Shouldly;
using ShipLean.Contracts.Agents;

namespace ShipLean.UnitTest.Agents;

[TestFixture]
public class AgentUrlsTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void AgentUrls_WhenBaseUrlUnset_ShouldUseRequestHost(string? baseUrl)
    {
        AgentUrls.Issuer(baseUrl, "http://127.0.0.1:5000").ShouldBe("http://127.0.0.1:5000");
        AgentUrls.Resource(baseUrl, "http://127.0.0.1:5000").ShouldBe("http://127.0.0.1:5000/mcp");
    }

    [TestCase("https://ship.example.test")]
    [TestCase("https://ship.example.test/")]
    [TestCase(" https://ship.example.test// ")]
    public void AgentUrls_WhenBaseUrlSet_ShouldUseItWithoutTrailingSlash(string baseUrl)
    {
        AgentUrls.Issuer(baseUrl, "http://127.0.0.1:5000").ShouldBe("https://ship.example.test");
        AgentUrls.Resource(baseUrl, "http://127.0.0.1:5000").ShouldBe("https://ship.example.test/mcp");
    }

    [Test]
    public void AgentUrls_ShouldPinTheEndpointPaths()
    {
        AgentUrls.AuthorizePath.ShouldBe("/oauth/authorize");
        AgentUrls.TokenPath.ShouldBe("/oauth/token");
        AgentUrls.RevokePath.ShouldBe("/oauth/revoke");
        AgentUrls.RegisterPath.ShouldBe("/oauth/register");
        AgentUrls.AuthorizationServerMetadataPath.ShouldBe("/.well-known/oauth-authorization-server");
        AgentUrls.ProtectedResourceMetadataPath.ShouldBe("/.well-known/oauth-protected-resource");
        AgentUrls.ConsentPagePath.ShouldBe("/authorize");
    }

    [TestCase("https://ship.example.test/app")]
    [TestCase("https://ship.example.test/app/")]
    [TestCase("https://ship.example.test:443/app/deep?x=1")]
    public void AgentUrls_WhenBaseUrlHasPath_ShouldUseOriginOnly(string baseUrl)
    {
        AgentUrls.Issuer(baseUrl, "http://127.0.0.1:5000").ShouldBe("https://ship.example.test");
        AgentUrls.Resource(baseUrl, "http://127.0.0.1:5000").ShouldBe("https://ship.example.test/mcp");
        AgentUrls.Issuer("http://ship.example.test:8080/app", "http://127.0.0.1:5000").ShouldBe("http://ship.example.test:8080");
    }

    [TestCase("https://ShipLean.Example:443")]
    [TestCase("https://SHIPLEAN.EXAMPLE")]
    [TestCase("https://shiplean.example:443/")]
    public void AgentUrls_WhenBaseUrlIsNotCanonical_ShouldAgreeWithTheCanonicalSpelling(string baseUrl)
    {
        AgentUrls.Issuer(baseUrl, "http://127.0.0.1:5000").ShouldBe("https://shiplean.example");
        AgentUrls.Resource(baseUrl, "http://127.0.0.1:5000").ShouldBe(AgentUrls.Resource("https://shiplean.example", "http://127.0.0.1:5000"));
    }

    [TestCase("http://ShipLean.Local")]
    [TestCase("http://shiplean.local:80")]
    public void AgentUrls_WhenRequestOriginIsNotCanonical_ShouldCanonicaliseIt(string requestOrigin)
    {
        AgentUrls.Issuer(null, requestOrigin).ShouldBe("http://shiplean.local");
    }
}
