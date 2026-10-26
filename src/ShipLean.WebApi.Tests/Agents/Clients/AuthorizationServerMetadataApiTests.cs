using System.Net;
using System.Text.Json;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Agents.Clients;

// The seed's base_url is https://shiplean.larkspur.dev, so that is the issuer.
[TestFixture]
public class AuthorizationServerMetadataApiTests : ApiFixture
{
    protected override TestUser SignedInAs => TestUser.Anonymous;

    private static string[] Strings(JsonElement element) => element.EnumerateArray().Select(item => item.GetString()!).ToArray();

    [Test]
    public async Task Metadata_ShouldAdvertiseCimdS256AndPublicClients()
    {
        using var response = await Client.GetAsync("/.well-known/oauth-authorization-server");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metadata = document.RootElement;
        metadata.GetProperty("issuer").GetString().ShouldBe("https://shiplean.larkspur.dev");
        metadata.GetProperty("authorization_endpoint").GetString().ShouldBe("https://shiplean.larkspur.dev/oauth/authorize");
        metadata.GetProperty("token_endpoint").GetString().ShouldBe("https://shiplean.larkspur.dev/oauth/token");
        metadata.GetProperty("revocation_endpoint").GetString().ShouldBe("https://shiplean.larkspur.dev/oauth/revoke");
        metadata.GetProperty("registration_endpoint").GetString().ShouldBe("https://shiplean.larkspur.dev/oauth/register");
        Strings(metadata.GetProperty("response_types_supported")).ShouldBe(["code"]);
        Strings(metadata.GetProperty("grant_types_supported")).ShouldBe(["authorization_code", "refresh_token"]);
        Strings(metadata.GetProperty("code_challenge_methods_supported")).ShouldBe(["S256"]);
        Strings(metadata.GetProperty("token_endpoint_auth_methods_supported")).ShouldBe(["none"]);
        metadata.GetProperty("client_id_metadata_document_supported").GetBoolean().ShouldBeTrue();
        metadata.GetProperty("authorization_response_iss_parameter_supported").GetBoolean().ShouldBeTrue();
    }
}
