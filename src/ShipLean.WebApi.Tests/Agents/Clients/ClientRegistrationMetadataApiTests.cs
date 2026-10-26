using System.Net;
using System.Text;
using System.Text.Json;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Agents.Clients;

// Its own fixture, so its host's per-IP registration budget is not shared with another class's requests.
[TestFixture]
public class ClientRegistrationMetadataApiTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    [TestCase("""{"redirect_uris":["https://agent.example.test/cb"]}""")]
    [TestCase("""{"client_name":"Bot","redirect_uris":["https://agent.example.test/cb"],"token_endpoint_auth_method":"client_secret_basic"}""")]
    [TestCase("""{"client_name":"Bot","redirect_uris":["https://agent.example.test/cb"],"grant_types":["password"]}""")]
    [TestCase("""not json""")]
    [TestCase("""["array"]""")]
    public async Task Register_WhenMetadataInvalid_ShouldAnswerInvalidClientMetadata(string json)
    {
        using var response = await Client.PostAsync("/oauth/register", new StringContent(json, Encoding.UTF8, "application/json"));
        var status = response.StatusCode;
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        status.ShouldBe(HttpStatusCode.BadRequest);
        body.GetProperty("error").GetString().ShouldBe("invalid_client_metadata");
    }
}
