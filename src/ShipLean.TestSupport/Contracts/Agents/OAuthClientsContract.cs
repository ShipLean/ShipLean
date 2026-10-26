using Shouldly;
using static ShipLean.TestSupport.Contracts.Agents.AgentWorld;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.TestSupport.Contracts.Agents;

// Behaviour every IOAuthClients has for dynamic client registration. Which metadata-document clients exist is the
// implementation's business, so a derivation tests those itself.
[TestFixture]
public abstract class OAuthClientsContract
{
    protected abstract AgentWorld World { get; }

    [Test]
    public async Task Register_ShouldReturnUnverifiedClientThatCanBeFound()
    {
        var client = await World.Clients.RegisterAsync(new OAuthClientRegistration("  My Agent  ", [LoopbackRedirect, "https://agent.example.test/callback"]));

        client.ClientId.ShouldNotBeNullOrWhiteSpace();
        client.Kind.ShouldBe(OAuthClientKind.Registered);
        client.ClientName.ShouldBe("My Agent");
        client.ClientHost.ShouldBeNull();
        client.RedirectUris.ShouldBe([LoopbackRedirect, "https://agent.example.test/callback"]);

        var found = await World.Clients.FindAsync(client.ClientId);
        found.ShouldNotBeNull();
        found.ClientId.ShouldBe(client.ClientId);
        found.Kind.ShouldBe(OAuthClientKind.Registered);
        found.ClientName.ShouldBe("My Agent");
        found.ClientHost.ShouldBeNull();
        found.Verified.ShouldBeFalse();
        found.RedirectUris.ShouldBe([LoopbackRedirect, "https://agent.example.test/callback"]);
    }

    [Test]
    public async Task Register_WhenTwice_ShouldGiveDifferentClientIds()
    {
        var first = await World.Clients.RegisterAsync(new OAuthClientRegistration("Agent", [LoopbackRedirect]));
        var second = await World.Clients.RegisterAsync(new OAuthClientRegistration("Agent", [LoopbackRedirect]));

        second.ClientId.ShouldNotBe(first.ClientId);
    }

    [TestCase("http://example.test/callback")]
    [TestCase("ftp://example.test/callback")]
    [TestCase("javascript:alert(1)")]
    [TestCase("/relative/callback")]
    [TestCase("not a uri")]
    [TestCase("https://agent.example.test/callback#fragment")]
    [TestCase("http://localhost./callback")]
    public async Task Register_WhenRedirectNotLoopbackOrHttps_ShouldBeValidation(string redirect)
    {
        var error = await Should.ThrowAsync<ApiException>(() => World.Clients.RegisterAsync(new OAuthClientRegistration("Agent", [LoopbackRedirect, redirect])));

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase("http://127.0.0.1:9000/cb")]
    [TestCase("http://[::1]:9000/cb")]
    [TestCase("http://localhost/cb")]
    [TestCase("https://agent.example.test/cb")]
    [TestCase("cursor://anysphere.cursor-retrieval/oauth/callback")]
    public async Task Register_WhenRedirectLoopbackHttpsOrPrivateUse_ShouldAccept(string redirect)
    {
        (await World.Clients.RegisterAsync(new OAuthClientRegistration("Agent", [redirect]))).RedirectUris.ShouldBe([redirect]);
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task Register_WhenNameBlank_ShouldBeValidation(string name)
    {
        (await Should.ThrowAsync<ApiException>(() => World.Clients.RegisterAsync(new OAuthClientRegistration(name, [LoopbackRedirect])))).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Register_WhenNoRedirects_ShouldBeValidation()
    {
        (await Should.ThrowAsync<ApiException>(() => World.Clients.RegisterAsync(new OAuthClientRegistration("Agent", [])))).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task Find_WhenUnknown_ShouldReturnNull()
    {
        (await World.Clients.FindAsync("no-such-client")).ShouldBeNull();
    }

    [TestCase("http://x@127.0.0.1:9000/cb")]
    [TestCase("http://user:pass@localhost/cb")]
    [TestCase("https://x@agent.example.test/cb")]
    [TestCase("https://agent.example.test@evil.example.test/cb")]
    public async Task Register_WhenRedirectHasUserInfo_ShouldBeValidation(string redirect)
    {
        (await Should.ThrowAsync<ApiException>(() => World.Clients.RegisterAsync(new OAuthClientRegistration("Agent", [redirect])))).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
