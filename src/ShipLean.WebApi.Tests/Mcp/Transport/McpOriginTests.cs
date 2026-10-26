using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// A browser's Origin must be the server's own (the seed's base_url) or the request is refused before anything else.
[TestFixture]
public class McpOriginTests : McpFixture
{
    [TestCase("https://evil.example")]
    [TestCase("null")]
    [TestCase("http://shiplean.larkspur.dev")]
    [TestCase("https://shiplean.larkspur.dev:8443")]
    [TestCase("https://shiplean.larkspur.dev.evil.example")]
    public async Task Mcp_WhenForeignOrigin_ShouldRefuse(string origin)
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "probe_whoami", configure: request => request.Headers.Add("Origin", origin));

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        reply.Json().GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Forbidden);
        Log.Runs("Whoami").ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenForeignOriginAndNoToken_ShouldRefuseAs403NotAChallenge()
    {
        var reply = await CallToolAsync(null, "probe_whoami", configure: request => request.Headers.Add("Origin", "https://evil.example"));

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    [TestCase("https://shiplean.larkspur.dev")]
    [TestCase("https://SHIPLEAN.larkspur.dev:443")]
    public async Task Mcp_WhenOwnOrigin_ShouldRun(string origin)
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "probe_whoami", configure: request => request.Headers.Add("Origin", origin));

        reply.Status.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Mcp_WhenNoOrigin_ShouldRun()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "probe_whoami");

        reply.Status.ShouldBe(HttpStatusCode.OK);
    }
}
