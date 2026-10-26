using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Agents.Clients;

// Dynamic client registration, anonymous. Each registration fixture stays under the per-IP limit on its own host.
[TestFixture]
public class ClientRegistrationApiTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    private async Task<(HttpStatusCode Status, JsonElement Body)> RegisterAsync(string json)
    {
        using var response = await Client.PostAsync("/oauth/register", new StringContent(json, Encoding.UTF8, "application/json"));
        return (response.StatusCode, JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone());
    }

    [Test]
    public async Task Register_WhenValid_ShouldReturnClientId()
    {
        var (status, body) = await RegisterAsync("""{"client_name":"Review Bot","redirect_uris":["http://127.0.0.1:8123/callback"],"token_endpoint_auth_method":"none"}""");

        status.ShouldBe(HttpStatusCode.Created);
        var clientId = body.GetProperty("client_id").GetString();
        clientId.ShouldNotBeNullOrWhiteSpace();
        body.GetProperty("client_name").GetString().ShouldBe("Review Bot");
        body.GetProperty("token_endpoint_auth_method").GetString().ShouldBe("none");
        body.TryGetProperty("client_secret", out _).ShouldBeFalse();
        var stored = await Host.Services.GetRequiredService<IOAuthClients>().FindAsync(clientId);
        stored.ShouldNotBeNull();
        stored.Kind.ShouldBe(OAuthClientKind.Registered);
        stored.RedirectUris.ShouldBe(["http://127.0.0.1:8123/callback"]);
    }

    [Test]
    public async Task Register_WhenRedirectIsLocalhostWithPort_ShouldBeAccepted()
    {
        var (status, body) = await RegisterAsync("""{"client_name":"Bot","redirect_uris":["http://localhost:3118/cb"]}""");

        status.ShouldBe(HttpStatusCode.Created);
        body.GetProperty("redirect_uris")[0].GetString().ShouldBe("http://localhost:3118/cb");
    }

    [TestCase("""{"client_name":"Bot","redirect_uris":["http://evil.example.test/cb"]}""")]
    [TestCase("""{"client_name":"Bot","redirect_uris":["https://user@agent.example.test/cb"]}""")]
    [TestCase("""{"client_name":"Bot","redirect_uris":["javascript:alert(1)"]}""")]
    [TestCase("""{"client_name":"Bot","redirect_uris":["https://agent.example.test/cb#frag"]}""")]
    [TestCase("""{"client_name":"Bot","redirect_uris":[]}""")]
    [TestCase("""{"client_name":"Bot"}""")]
    public async Task Register_WhenRedirectNotLoopbackOrHttps_ShouldBeValidation(string json)
    {
        var (status, body) = await RegisterAsync(json);

        status.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("invalid_redirect_uri");
        body.GetProperty("error_description").GetString().ShouldNotBeNullOrWhiteSpace();
    }
}
