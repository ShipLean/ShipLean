using Shouldly;
using ShipLean.Application.Fakes.Agents;
using ShipLean.Contracts.Agents;

namespace ShipLean.UnitTest.Agents;

[TestFixture]
public class OAuthClientTests
{
    private static OAuthClient Client(params string[] redirects) => new("client", OAuthClientKind.Registered, "Client", null, redirects);

    [Test]
    public void AllowsRedirect_WhenExactMatch_ShouldAllow()
    {
        Client("https://agent.example.test/cb").AllowsRedirect("https://agent.example.test/cb").ShouldBeTrue();
    }

    [Test]
    public void AllowsRedirect_WhenLoopbackOtherPort_ShouldAllow()
    {
        var client = Client("http://127.0.0.1:8123/callback?x=1");

        client.AllowsRedirect("http://127.0.0.1:54321/callback?x=1").ShouldBeTrue();
        Client("http://localhost/cb").AllowsRedirect("http://localhost:3000/cb").ShouldBeTrue();
        Client("http://[::1]:1/cb").AllowsRedirect("http://[::1]:2/cb").ShouldBeTrue();
    }

    [Test]
    public void AllowsRedirect_WhenCustomScheme_ShouldMatchExactly()
    {
        var client = Client("cursor://anysphere.cursor-retrieval/oauth/callback");

        client.AllowsRedirect("cursor://anysphere.cursor-retrieval/oauth/callback").ShouldBeTrue();
        client.AllowsRedirect("cursor://anysphere.cursor-retrieval/oauth/callback?x=1").ShouldBeFalse();
        client.AllowsRedirect("cursor://anysphere.cursor-retrieval:99/oauth/callback").ShouldBeFalse();
        client.AllowsRedirect("cursor://other/oauth/callback").ShouldBeFalse();
        OAuthClient.IsLoopback("cursor://localhost/cb").ShouldBeFalse();
    }

    [Test]
    public void AllowsRedirect_WhenLoopbackPathQueryOrHostDiffers_ShouldRefuse()
    {
        var client = Client("http://127.0.0.1:8123/callback");

        client.AllowsRedirect("http://127.0.0.1:9/other").ShouldBeFalse();
        client.AllowsRedirect("http://127.0.0.1:9/callback?x=1").ShouldBeFalse();
        client.AllowsRedirect("http://localhost:9/callback").ShouldBeFalse();
        client.AllowsRedirect("https://127.0.0.1:9/callback").ShouldBeFalse();
        client.AllowsRedirect("http://127.0.0.1:9/callback#frag").ShouldBeFalse();
    }

    [Test]
    public void AllowsRedirect_WhenHttpsPathDiffers_ShouldRefuse()
    {
        var client = Client("https://agent.example.test/cb");

        client.AllowsRedirect("https://agent.example.test/other").ShouldBeFalse();
        client.AllowsRedirect("https://agent.example.test:8443/cb").ShouldBeFalse();
        client.AllowsRedirect("https://evil.example.test/cb").ShouldBeFalse();
    }

    [Test]
    public void AllowsRedirect_WhenHostIsNotLoopbackOnHttp_ShouldRefuse()
    {
        Client("http://example.test:80/cb").AllowsRedirect("http://example.test:81/cb").ShouldBeFalse();
        Client("http://127.0.0.1/cb").AllowsRedirect("http://127.0.0.1.evil.test/cb").ShouldBeFalse();
    }

    [Test]
    public async Task Find_WhenSeeded_ShouldKnowClaudeCodeAndTestAgent()
    {
        var clients = new InMemoryOAuthClients(new InMemoryAgentStore());

        var claude = (await clients.FindAsync("https://claude.ai/oauth/claude-code-client-metadata"))!;
        claude.ClientName.ShouldBe("Claude Code");
        claude.Kind.ShouldBe(OAuthClientKind.MetadataDocument);
        claude.ClientHost.ShouldBe("claude.ai");
        claude.RedirectUris.ShouldContain(uri => uri.StartsWith("https://claude.ai/", StringComparison.Ordinal));
        claude.RedirectUris.ShouldContain(uri => OAuthClient.IsLoopback(uri));

        var test = (await clients.FindAsync("test-agent"))!;
        test.ClientName.ShouldBe("Test Agent");
        test.Kind.ShouldBe(OAuthClientKind.Registered);
        test.ClientHost.ShouldBeNull();
        test.RedirectUris.ShouldAllBe(uri => OAuthClient.IsLoopback(uri));
    }

    [TestCase("http://x@127.0.0.1:9/cb")]
    [TestCase("http://user:pass@127.0.0.1:9/cb")]
    [TestCase("http://127.0.0.1@evil.example.test/cb")]
    [TestCase("https://x@agent.example.test/cb")]
    public void AllowsRedirect_WhenUserInfo_ShouldRefuse(string redirect)
    {
        Client("http://127.0.0.1:8123/cb", "https://agent.example.test/cb", redirect).AllowsRedirect(redirect).ShouldBeFalse();
    }

    [Test]
    public void AllowsRedirect_WhenLocalhostTrailingDot_ShouldRefuse()
    {
        Client("http://localhost/cb").AllowsRedirect("http://localhost.:3000/cb").ShouldBeFalse();
        Client("http://localhost./cb").AllowsRedirect("http://localhost.:3000/cb").ShouldBeFalse();
    }

    [Test]
    public void Verified_ShouldBeTrueOnlyForMetadataDocumentClientWithHost()
    {
        new OAuthClient("a", OAuthClientKind.MetadataDocument, "A", "a.example.test", ["https://a.example.test/cb"]).Verified.ShouldBeTrue();
        new OAuthClient("a", OAuthClientKind.MetadataDocument, "A", null, ["https://a.example.test/cb"]).Verified.ShouldBeFalse();
        new OAuthClient("a", OAuthClientKind.Registered, "A", "a.example.test", ["https://a.example.test/cb"]).Verified.ShouldBeFalse();
        Client("https://a.example.test/cb").Verified.ShouldBeFalse();
    }
}
