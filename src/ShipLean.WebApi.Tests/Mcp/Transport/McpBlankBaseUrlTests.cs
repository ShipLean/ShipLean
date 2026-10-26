using System.Net;
using Dapper;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// With no base_url the server only knows its own name from the Host header, which a DNS-rebinding page controls, so a
// browser Origin cannot be verified and is refused. An agent sends no Origin and is not affected.
[TestFixture]
public class McpBlankBaseUrlTests : McpFixture
{
    [TearDown]
    public async Task RestoreBaseUrl() => await SetBaseUrlAsync("https://shiplean.larkspur.dev");

    [Test]
    public async Task Mcp_WhenBaseUrlBlankAndRequestHasOrigin_ShouldRefuse()
    {
        await SetBaseUrlAsync("");
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "probe_whoami", configure: request => request.Headers.Add("Origin", Client.BaseAddress!.GetLeftPart(UriPartial.Authority)));

        reply.Status.ShouldBe(HttpStatusCode.Forbidden);
        Log.Runs("Whoami").ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenBaseUrlBlankAndRequestHasNoOrigin_ShouldRun()
    {
        await SetBaseUrlAsync("");
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "probe_whoami");

        reply.Status.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SetBaseUrlAsync(string baseUrl)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE instance_settings SET base_url = @baseUrl", new { baseUrl });
    }
}
