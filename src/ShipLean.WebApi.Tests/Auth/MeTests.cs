using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Schema;

namespace ShipLean.WebApi.Tests.Auth;

// GET /api/me and GET /api/auth/instance.
[TestFixture]
public class MeTests : ApiFixture
{
    [Test]
    public async Task Me_WhenSignedIn_ShouldMatchThePinnedShape()
    {
        var body = await Api.GetJsonAsync<JsonNode>("/api/me");

        JsonNode.DeepEquals(body, PinnedFiles.Read("me.json")["me"]).ShouldBeTrue(body.ToJsonString());
    }

    [Test]
    public async Task Me_WhenSignedInAsAnotherUser_ShouldDescribeThem()
    {
        var body = await Api.ActingAs(TestUser.Kai).GetJsonAsync<JsonNode>("/api/me");

        body["id"]!.GetValue<Guid>().ShouldBe(TestUser.Kai.Id!.Value);
        body["hasProjects"]!.GetValue<bool>().ShouldBeFalse();
        body["isInstanceAdmin"]!.GetValue<bool>().ShouldBeFalse();
        body["canCreateProjects"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Test]
    public async Task Me_WhenAnonymous_ShouldAnswer401()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).GetAsync("/api/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Instance_WhenAnonymous_ShouldReturnName()
    {
        using var response = await Api.ActingAs(TestUser.Anonymous).GetAsync("/api/auth/instance");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString().ShouldBe("Larkspur Engineering");
    }
}
