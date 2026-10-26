using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Agents.Authorize;

// The protocol errors /oauth/authorize sends back to a loopback redirect.
[TestFixture]
public sealed class AuthorizeRefusalTests : AuthorizeFixtureBase
{
    private async Task<Uri> RefusalAsync(string clientId, params (string Name, string? Value)[] overrides)
    {
        using var browser = await SignedInBrowserAsync();
        using var response = await browser.GetAsync(AuthorizeUrl(clientId, overrides));
        var location = Location(response);
        location.GetLeftPart(UriPartial.Path).ShouldBe(Redirect);
        QueryOf(location)["iss"].ToString().ShouldBe(Issuer);
        return location;
    }

    [TestCase("plain")]
    [TestCase(null)]
    public async Task Authorize_WhenNoPkceOrPlain_ShouldRefuse(string? method)
    {
        var clientId = await ArrangeClientAsync();

        var location = await RefusalAsync(clientId, ("code_challenge_method", method));

        QueryOf(location)["error"].ToString().ShouldBe("invalid_request");
        QueryOf(location)["state"].ToString().ShouldBe("st-1");
    }

    [Test]
    public async Task Authorize_WhenNoChallenge_ShouldRefuse()
    {
        var clientId = await ArrangeClientAsync();

        var location = await RefusalAsync(clientId, ("code_challenge", null));

        QueryOf(location)["error"].ToString().ShouldBe("invalid_request");
    }

    [TestCase(null)]
    [TestCase("https://shiplean.larkspur.dev")]
    [TestCase("https://elsewhere.example/mcp")]
    public async Task Authorize_WhenResourceNotMcp_ShouldRefuse(string? resource)
    {
        var clientId = await ArrangeClientAsync();

        var location = await RefusalAsync(clientId, ("resource", resource));

        QueryOf(location)["error"].ToString().ShouldBe("invalid_target");
    }

    [Test]
    public async Task Authorize_WhenResponseTypeIsNotCode_ShouldRefuse()
    {
        var clientId = await ArrangeClientAsync();

        var location = await RefusalAsync(clientId, ("response_type", "token"));

        QueryOf(location)["error"].ToString().ShouldBe("unsupported_response_type");
    }

    [Test]
    public async Task Authorize_WhenScopeUnknown_ShouldRefuseWithoutEchoingIt()
    {
        var clientId = await ArrangeClientAsync();

        var location = await RefusalAsync(clientId, ("scope", "shiplean:read shiplean:admin"));

        QueryOf(location)["error"].ToString().ShouldBe("invalid_scope");
        QueryOf(location)["error_description"].ToString().ShouldNotContain("shiplean:admin");
    }
}
