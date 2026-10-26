using System.Net;
using Dapper;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// A token is bound to the /mcp URL of the base_url it was issued under; the account behind it is checked on every call.
[TestFixture]
public class McpAudienceTests : McpFixture
{
    [Test]
    public async Task Mcp_WhenWrongAudience_ShouldAnswer401()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        (await CallToolAsync(token, "probe_whoami")).Status.ShouldBe(HttpStatusCode.OK);

        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE instance_settings SET base_url = 'https://moved.example'");
        }
        var reply = await CallToolAsync(token, "probe_whoami");
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE instance_settings SET base_url = 'https://shiplean.larkspur.dev'");
        }

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Mcp_WhenUserDisabled_ShouldAnswer401()
    {
        var token = await TokenAsync(TestUser.Kai, ReadOnly);
        (await CallToolAsync(token, "probe_whoami")).Status.ShouldBe(HttpStatusCode.OK);

        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE app_user SET disabled_at = now() WHERE id = @id", new { id = TestUser.Kai.Id });
        }
        var reply = await CallToolAsync(token, "probe_whoami");

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestCase("https://ShipLean.Larkspur.dev")]
    [TestCase("https://shiplean.larkspur.dev:443")]
    [TestCase("https://shiplean.larkspur.dev/")]
    [TestCase("https://shiplean.larkspur.dev/app")]
    public async Task Mcp_WhenBaseUrlIsNotCanonical_ShouldAcceptATokenIssuedForTheCanonicalAudience(string stored)
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE instance_settings SET base_url = @stored", new { stored });
        }
        try
        {
            var token = await TokenAsync(TestUser.Joel, ReadOnly);

            var reply = await CallToolAsync(token, "probe_whoami", configure: request => request.Headers.Add("Origin", "https://shiplean.larkspur.dev"));

            reply.Status.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            await using var connection = await OpenDatabaseAsync();
            await connection.ExecuteAsync("UPDATE instance_settings SET base_url = 'https://shiplean.larkspur.dev'");
        }
    }
}
