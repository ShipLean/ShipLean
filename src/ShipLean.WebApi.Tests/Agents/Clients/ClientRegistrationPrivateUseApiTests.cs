using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Agents.Clients;

// Private-use scheme redirects (RFC 8252 7.1) at dynamic client registration. Its own fixture: each one stays under the per-IP limit.
[TestFixture]
public class ClientRegistrationPrivateUseApiTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    private async Task<(HttpStatusCode Status, JsonElement Body)> RegisterAsync(string json)
    {
        using var response = await Client.PostAsync("/oauth/register", new StringContent(json, Encoding.UTF8, "application/json"));
        return (response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone());
    }

    [Test]
    public async Task Register_WhenPrivateUseSchemeRedirect_ShouldReturnClientId()
    {
        var (status, body) = await RegisterAsync("""{"client_name":"Cursor","redirect_uris":["cursor://anysphere.cursor-retrieval/oauth/callback"],"token_endpoint_auth_method":"none"}""");

        status.ShouldBe(HttpStatusCode.Created);
        var clientId = body.GetProperty("client_id").GetString();
        clientId.ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("redirect_uris")[0].GetString().ShouldBe("cursor://anysphere.cursor-retrieval/oauth/callback");
        var stored = await Host.Services.GetRequiredService<IOAuthClients>().FindAsync(clientId);
        stored!.RedirectUris.ShouldBe(["cursor://anysphere.cursor-retrieval/oauth/callback"]);
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("data:text/html,x")]
    [TestCase("file:///etc/passwd")]
    [TestCase("vbscript:x")]
    [TestCase("blob:https://a.example/x")]
    [TestCase("ftp://a.example/x")]
    [TestCase("mailto:a@b.example")]
    [TestCase("cursor://user@host/cb")]
    [TestCase("cursor://host/cb#frag")]
    public async Task Register_WhenSchemeBlockedOrUserinfoOrFragment_ShouldBeInvalidRedirectUri(string redirect)
    {
        var (status, body) = await RegisterAsync($$"""{"client_name":"Bot","redirect_uris":["{{redirect}}"]}""");

        status.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("invalid_redirect_uri");
    }
}
