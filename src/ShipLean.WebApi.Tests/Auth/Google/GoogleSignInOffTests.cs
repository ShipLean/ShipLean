using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Auth.Google;

namespace ShipLean.WebApi.Tests.Auth.Google;

// With no SHIPLEAN_GOOGLE_* setting nothing about Google is reachable, and every other request still works, which it
// wouldn't if the scheme's handler were built without a client id.
[TestFixture]
public class GoogleSignInOffTests : ApiFixture
{
    protected override bool UseTestSignIn => false;

    protected override TestUser SignedInAs => TestUser.Anonymous;

    [Test]
    public async Task Instance_WhenGoogleIsOff_ShouldSaySo()
    {
        var instance = await Client.GetFromJsonAsync<JsonNode>("/api/auth/instance");

        instance!["googleSignIn"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Test]
    public async Task Start_WhenGoogleIsOff_ShouldBeNotFound()
    {
        using var response = await Client.GetAsync("/api/auth/google/start");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Callback_WhenGoogleIsOff_ShouldNotBeHandled()
    {
        using var response = await Client.GetAsync($"{GoogleSignInServices.CallbackPath}?code=x&state=y");

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }
}
