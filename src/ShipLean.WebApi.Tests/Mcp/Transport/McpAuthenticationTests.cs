using System.Net;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// Who gets into /mcp: a valid agent bearer and nobody else. The seed's base_url is https://shiplean.larkspur.dev.
[TestFixture]
public class McpAuthenticationTests : McpFixture
{
    private const string MetadataUrl = "https://shiplean.larkspur.dev/.well-known/oauth-protected-resource/mcp";

    [Test]
    public async Task Mcp_WhenNoToken_ShouldAnswer401WithResourceMetadataAndReadWriteScope()
    {
        var reply = await CallToolAsync(null, "probe_whoami");

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
        reply.WwwAuthenticate.ShouldContain($"resource_metadata=\"{MetadataUrl}\"");
        reply.WwwAuthenticate.ShouldContain("scope=\"shiplean:read shiplean:write\"");
        reply.Headers.Location.ShouldBeNull();
        reply.Json().GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Unauthorized);
    }

    [Test]
    public async Task Mcp_WhenEmailHeaderAndNoToken_ShouldAnswer401()
    {
        const string email = "ignored.header@example.test";

        var reply = await CallToolAsync(null, "probe_whoami", "{}", request => request.Headers.TryAddWithoutValidation("x-user-email", email));

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
        reply.WwwAuthenticate.ShouldNotContain("invalid_token");
        Log.Runs("Whoami").ShouldBe(0);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM app_user WHERE lower(email) = lower(@email)", new { email })).ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenTokenUnknown_ShouldAnswer401AsInvalidToken()
    {
        var reply = await CallToolAsync("not-a-real-token", "probe_whoami");

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
        reply.WwwAuthenticate.ShouldContain("error=\"invalid_token\"");
        reply.WwwAuthenticate.ShouldContain($"resource_metadata=\"{MetadataUrl}\"");
    }

    [Test]
    public async Task Mcp_WhenCookieOnly_ShouldAnswer401()
    {
        using var browser = await AuthClient.SignedInAsync(Host, TestUser.Joel.Email!, TestUser.Joel.Password!);
        using (var me = await browser.GetAsync("/api/me"))
        {
            me.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("""{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"probe_whoami","arguments":{}}}""", System.Text.Encoding.UTF8, "application/json"),
        };
        using var response = await browser.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        Log.Runs("Whoami").ShouldBe(0);
    }

    [Test]
    public async Task Api_WhenBearerOnly_ShouldAnswer401()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new("Bearer", token);

        using var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Hubs_WhenBearerOnly_ShouldAnswer401()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWritePlan);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/hubs/project/negotiate?negotiateVersion=1");
        request.Headers.Authorization = new("Bearer", token);

        using var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Mcp_WhenTwoTokensOneConnection_ShouldActAsTwoUsers()
    {
        var mei = await TokenAsync(TestUser.Mei, ReadOnly);
        var priya = await TokenAsync(TestUser.Priya, ReadOnly);

        var first = await CallToolAsync(mei, "probe_whoami");
        var second = await CallToolAsync(priya, "probe_whoami");
        var third = await CallToolAsync(mei, "probe_whoami");

        first.ResultText().ShouldStartWith(TestUser.Mei.Id.ToString()!);
        second.ResultText().ShouldStartWith(TestUser.Priya.Id.ToString()!);
        third.ResultText().ShouldStartWith(TestUser.Mei.Id.ToString()!);
    }

    [Test]
    public async Task Mcp_WhenAgentCalls_ShouldSetCurrentAgent()
    {
        var token = await TokenAsync(TestUser.Tom, ReadOnly, "Review Bot");

        var reply = await CallToolAsync(token, "probe_whoami");

        reply.ResultText().ShouldBe($"{TestUser.Tom.Id}|Review Bot||False");
    }

    [Test]
    public async Task Mcp_WhenGetOrDelete_ShouldAnswer405()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Delete })
        {
            using var request = new HttpRequestMessage(method, "/mcp");
            var reply = await SendAsync(request, token);

            reply.Status.ShouldBe(HttpStatusCode.MethodNotAllowed, method.Method);
        }
    }

    [Test]
    public async Task Mcp_WhenGetWithoutToken_ShouldAnswer401NotARedirectToLogin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/mcp");

        var reply = await SendAsync(request, null);

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
        reply.WwwAuthenticate.ShouldContain("resource_metadata=");
    }

    [Test]
    public async Task ProtectedResourceMetadata_ShouldNameIssuerAndScopes()
    {
        foreach (var path in new[] { "/.well-known/oauth-protected-resource", "/.well-known/oauth-protected-resource/mcp" })
        {
            using var response = await Client.GetAsync(path);
            var metadata = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

            response.StatusCode.ShouldBe(HttpStatusCode.OK, path);
            metadata.GetProperty("resource").GetString().ShouldBe("https://shiplean.larkspur.dev/mcp");
            metadata.GetProperty("authorization_servers").EnumerateArray().Select(e => e.GetString()).ShouldBe(["https://shiplean.larkspur.dev"]);
            metadata.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()).ShouldBe(["shiplean:read", "shiplean:write", "shiplean:plan"]);
        }
    }
}
