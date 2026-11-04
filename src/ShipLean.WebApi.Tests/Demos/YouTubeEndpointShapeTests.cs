using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Demos;

// K5's YouTube rows over an explicit fake IYouTubeConnections: the stubs are never asserted.
[TestFixture]
public class YouTubeEndpointShapeTests : ApiFixture
{
    private static readonly string Project = $"/api/projects/{SeedIds.Projects.Ship}/youtube";
    private static readonly FakeYouTubeConnections Fake = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IYouTubeConnections>();
        services.AddSingleton<IYouTubeConnections>(Fake);
    };

    [SetUp]
    public void ResetFake() => Fake.Reset();

    [Test]
    public async Task Start_ShouldMatchThePinnedShape()
    {
        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/start");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Body(response)).ToJsonString().ShouldBe("""{"url":"https://accounts.example.test/consent"}""");
    }

    [Test]
    public async Task Check_WhenRefused_ShouldBeOkWithTheRefusal()
    {
        const string Refusal = "YouTube gave the test video private visibility, not the unlisted you chose.";
        Fake.Check = new YouTubeProjectDto(YouTubeConnectionStatus.Pending, "Channel", null, null, Refusal);

        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/check", new JsonObject { ["visibility"] = "unlisted" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await Body(response);
        body["status"]!.GetValue<string>().ShouldBe("pending");
        body["refusal"]!.GetValue<string>().ShouldBe(Refusal);
        body.AsObject().Select(p => p.Key).Order().ShouldBe(PinnedKeys("project-demo-settings.json", "youTube"));
        Fake.CheckedVisibility.ShouldBe(YouTubeVisibility.Unlisted);
    }

    [Test]
    public async Task Check_WhenNoChannel_ShouldBeConflict()
    {
        Fake.Throws = ApiException.Conflict("There is no YouTube channel to check.");

        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/check", new JsonObject { ["visibility"] = "unlisted" });

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
    }

    [Test]
    public async Task Disconnect_ShouldMatchThePinnedShape()
    {
        var pinned = JsonNode.Parse(await File.ReadAllTextAsync(PinnedPath("youtube-disconnect.json")))!;
        Fake.Disconnect = new YouTubeDisconnectDto(pinned["revoked"]!.GetValue<bool>(), pinned["message"]!.GetValue<string>());

        using var response = await Api.ActingAs(TestUser.Joel).PostJsonAsync($"{Project}/disconnect");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode.DeepEquals(await Body(response), pinned).ShouldBeTrue();
    }

    [TestCase("POST", "/start")]
    [TestCase("POST", "/check")]
    [TestCase("POST", "/disconnect")]
    [TestCase("GET", "/callback")]
    public async Task Routes_WhenOff_ShouldBeNotFound(string method, string suffix)
    {
        Fake.Throws = ApiException.NotFound("YouTube isn't turned on for this instance.");
        var path = suffix == "/callback" ? "/api/youtube/callback" : $"{Project}{suffix}";
        var client = Api.ActingAs(TestUser.Joel);

        JsonObject? body = suffix == "/check" ? new JsonObject { ["visibility"] = "unlisted" } : null;

        using var response = method == "GET" ? await client.GetAsync(path) : await client.PostJsonAsync(path, body);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Callback_WhenSignedOut_ShouldBeAFoundRedirectWithNoSignInChallenge()
    {
        Fake.CallbackPath = "/?youtube=error";

        using var response = await NoRedirects().ActingAs(TestUser.Anonymous).GetAsync("/api/youtube/callback?code=c&state=s");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.ShouldBe("/?youtube=error");
        Fake.Callbacks.ShouldBe(1);
        Fake.CallbackUser.ShouldBeNull();
    }

    [Test]
    public async Task Callback_WhenSignedIn_ShouldPassTheUser()
    {
        using var response = await NoRedirects().ActingAs(TestUser.Joel).GetAsync("/api/youtube/callback?code=c&state=s");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.OriginalString.ShouldBe("/p/SHIP/settings/demos?youtube=pending");
        Fake.CallbackUser.ShouldBe(SeedIds.Users.Joel);
    }

    // The fixture's client follows redirects; the callback's answer is the redirect itself.
    private ApiClient NoRedirects()
        => new(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(Host.BaseUrl) });

    private static async Task<JsonNode> Body(HttpResponseMessage response)
        => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static string PinnedPath(string name)
        => Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "demos", name);

    private static string[] PinnedKeys(string file, string property)
        => JsonNode.Parse(File.ReadAllText(PinnedPath(file)))![property]!.AsObject().Select(p => p.Key).Order().ToArray();
}
