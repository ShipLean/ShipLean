using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using Shouldly;

namespace ShipLean.WebApi.Tests.Demos;

// K5's non-YouTube demo routes, pinned over explicit fakes (DemoEndpointFakes), never over the stubs: every success body equals the pinned JSON in
// DtoFixtures/demos, and every error row has its status, code and field. One host for the class.
[TestFixture]
public class DemoEndpointShapeTests : ApiFixture
{
    private const string SafeCsp = "sandbox; default-src 'none'; style-src 'unsafe-inline'; img-src data:";

    private static readonly JsonSerializerOptions Wire = WireOptions();

    private static readonly string Sprint = SeedIds.Sprints.Sprint22.ToString();

    private static readonly string Project = SeedIds.Projects.Ship.ToString();

    private readonly DemoEndpointFakes _fakes = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => _fakes.Install;

    [SetUp]
    public void ClearFakes() => _fakes.Clear();

    private static JsonSerializerOptions WireOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    private static JsonNode Pinned(string file, params string[] path)
    {
        var node = JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "demos", file)))!;
        return path.Aggregate(node, (current, step) => current[step]!);
    }

    private static T Dto<T>(JsonNode pinned) => pinned.Deserialize<T>(Wire)!;

    private static async Task<JsonNode> BodyOfAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonNode.Parse(text)!;
    }

    private static async Task<ApiError> ErrorOfAsync(HttpResponseMessage response, HttpStatusCode status) => await ApiClient.ReadApiErrorAsync(response, status);

    private static BlobContent Svg() => new(new MemoryStream(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")), "image/svg+xml");

    private Task<HttpResponseMessage> SendFileAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "file.bin" } },
        };
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        return Api.Http.SendAsync(request);
    }

    [Test]
    public async Task GetDemo_ShouldMatchThePinnedShape()
    {
        var pinned = Pinned("sprint-demo-ready.json");
        _fakes.Demos.Get.Returns(Dto<SprintDemoDto>(pinned));

        using var response = await Api.GetAsync($"api/sprints/{Sprint}/demo");

        JsonNode.DeepEquals(await BodyOfAsync(response), pinned).ShouldBeTrue();
    }

    [Test]
    public async Task GetDemo_WhenMissing_ShouldBeNotFound()
    {
        _fakes.Demos.Get.Throws(ApiException.NotFound("That sprint doesn't exist."));

        using var response = await Api.GetAsync($"api/sprints/{Sprint}/demo");

        var error = await ErrorOfAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Field.ShouldBe(null);
    }

    [Test]
    public async Task CreateDemo_ShouldMatchThePinnedShape()
    {
        var pinned = Pinned("sprint-demo-working.json");
        _fakes.Demos.Create.Returns(Dto<SprintDemoDto>(pinned));

        using var response = await Api.PostJsonAsync($"api/sprints/{Sprint}/demo");

        JsonNode.DeepEquals(await BodyOfAsync(response), pinned).ShouldBeTrue();
    }

    [Test]
    public async Task CreateDemo_WhenNoConnection_ShouldBeValidation()
    {
        _fakes.Demos.Create.Throws(ApiException.Validation("The project has no agent connection."));

        using var response = await Api.PostJsonAsync($"api/sprints/{Sprint}/demo");

        var error = await ErrorOfAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe(null);
    }

    [Test]
    public async Task CreateDemo_WhenLive_ShouldBeConflict()
    {
        _fakes.Demos.Create.Throws(ApiException.Conflict("A demo is already being made for this sprint."));

        using var response = await Api.PostJsonAsync($"api/sprints/{Sprint}/demo");

        var error = await ErrorOfAsync(response, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Field.ShouldBe(null);
    }

    [Test]
    public async Task Retry_WhenNotFailed_ShouldBeConflict()
    {
        _fakes.Demos.Retry.Throws(ApiException.Conflict("The YouTube upload hasn't failed."));

        using var response = await Api.PostJsonAsync($"api/sprints/{Sprint}/demo/youtube/retry");

        var error = await ErrorOfAsync(response, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Field.ShouldBe(null);
    }

    [Test]
    public async Task GetSettings_ShouldMatchThePinnedShape()
    {
        var pinned = Pinned("project-demo-settings-branded.json");
        _fakes.Settings.Get.Returns(Dto<ProjectDemoSettingsDto>(pinned));

        using var response = await Api.GetAsync($"api/projects/{Project}/demo-settings");

        JsonNode.DeepEquals(await BodyOfAsync(response), pinned).ShouldBeTrue();
    }

    [Test]
    public async Task PutSettings_WhenAutomaticBlocked_ShouldNameTheField()
    {
        _fakes.Settings.Save.Throws(ApiException.Validation("Automatic demos need an agent connection.", "automatic"));

        using var response = await Api.PutJsonAsync($"api/projects/{Project}/demo-settings", new { automatic = true, environment = "" });

        var error = await ErrorOfAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("automatic");
    }

    [Test]
    public async Task PutSettings_WhenEnvironmentTooLong_ShouldNameTheField()
    {
        _fakes.Settings.Save.Throws(ApiException.Validation("The environment is too long.", "environment"));

        using var response = await Api.PutJsonAsync($"api/projects/{Project}/demo-settings", new { automatic = false, environment = new string('x', 4001) });

        var error = await ErrorOfAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("environment");
    }

    [Test]
    public async Task GetLogo_ShouldCarryTheSafeHeaders()
    {
        var branded = Dto<ProjectDemoSettingsDto>(Pinned("project-demo-settings-branded.json")).Brand;
        _fakes.Brand.OpenLogo.Returns(Svg);
        _fakes.Brand.Read.Returns(branded);

        using var current = await Api.GetAsync($"api/projects/{Project}/demo-brand/logo?v=3");
        using var older = await Api.GetAsync($"api/projects/{Project}/demo-brand/logo?v=2");

        current.StatusCode.ShouldBe(HttpStatusCode.OK);
        current.Content.Headers.ContentType!.MediaType.ShouldBe("image/svg+xml");
        current.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        current.Headers.GetValues("Cross-Origin-Resource-Policy").ShouldBe(["same-origin"]);
        current.Headers.GetValues("Content-Security-Policy").Single().ShouldStartWith(SafeCsp);
        current.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        current.Headers.CacheControl!.ToString().ShouldBe("max-age=31536000, private, immutable");
        older.Headers.CacheControl!.ToString().ShouldBe("no-cache, private");
    }

    [Test]
    public async Task GetLogo_WhenNone_ShouldBeNotFound()
    {
        _fakes.Brand.OpenLogo.Throws(ApiException.NotFound("This project has no logo."));

        using var response = await Api.GetAsync($"api/projects/{Project}/demo-brand/logo");

        var error = await ErrorOfAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Field.ShouldBe(null);
    }

    [Test]
    public async Task PutLogo_ShouldMatchTheBrandShape()
    {
        var pinned = Pinned("project-demo-settings-branded.json", "brand");
        _fakes.LogoUpload.Replace.Returns(Dto<ProjectDemoBrandDto>(pinned));

        using var response = await SendFileAsync(HttpMethod.Put, $"api/projects/{Project}/demo-brand/logo");

        JsonNode.DeepEquals(await BodyOfAsync(response), pinned).ShouldBeTrue();
    }

    [Test]
    public async Task PutLogo_WhenRefused_ShouldNameTheField()
    {
        _fakes.LogoUpload.Replace.Throws(ApiException.Validation("A logo must be a PNG, JPEG, WebP or SVG image.", "logo"));

        using var response = await SendFileAsync(HttpMethod.Put, $"api/projects/{Project}/demo-brand/logo");

        var error = await ErrorOfAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("logo");
    }

    [Test]
    public async Task DeleteLogo_ShouldMatchTheBrandShape()
    {
        var pinned = Pinned("project-demo-settings.json", "brand");
        _fakes.Brand.RemoveLogo.Returns(Dto<ProjectDemoBrandDto>(pinned));

        using var response = await Api.DeleteAsync($"api/projects/{Project}/demo-brand/logo");

        JsonNode.DeepEquals(await BodyOfAsync(response), pinned).ShouldBeTrue();
    }

    [Test]
    public async Task PutTokens_ShouldMatchTheBrandShape()
    {
        var pinned = Pinned("project-demo-settings-branded.json", "brand");
        _fakes.Brand.SaveTokens.Returns(Dto<ProjectDemoBrandDto>(pinned));

        using var response = await Api.PutJsonAsync($"api/projects/{Project}/demo-brand/tokens", new { tokens = "--color-accent: #c2410c;" });

        JsonNode.DeepEquals(await BodyOfAsync(response), pinned).ShouldBeTrue();
    }

    [Test]
    public async Task PutTokens_WhenRefused_ShouldNameTheField()
    {
        _fakes.Brand.SaveTokens.Throws(ApiException.Validation("Line 2: url() isn't allowed.", "tokens"));

        using var response = await Api.PutJsonAsync($"api/projects/{Project}/demo-brand/tokens", new { tokens = "--color-bg: url(x);" });

        var error = await ErrorOfAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("tokens");
        error.Error.ShouldContain("Line 2");
    }

    [Test]
    public async Task DownloadLogo_ShouldBeAnAttachmentWithTheSafeHeaders()
    {
        _fakes.LogoDownload.Open.Returns(Svg);

        using var response = await Api.ActingAs(TestUser.Anonymous).GetAsync("api/downloads/demo-logo?token=live-token");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _fakes.LogoDownload.LastToken.ShouldBe("live-token");
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/svg+xml");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("Cross-Origin-Resource-Policy").ShouldBe(["same-origin"]);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldStartWith(SafeCsp);
        response.Headers.CacheControl!.ToString().ShouldBe("no-cache, private");
    }

    [Test]
    public async Task DownloadLogo_WhenTokenDead_ShouldBeNotFound()
    {
        _fakes.LogoDownload.Open.Throws(ApiException.NotFound("That download link is not valid."));

        using var response = await Api.ActingAs(TestUser.Anonymous).GetAsync("api/downloads/demo-logo?token=dead-token");

        var error = await ErrorOfAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Field.ShouldBe(null);
    }

    [Test]
    public async Task Upload_WhenAccepted_ShouldMatchThePinnedShape()
    {
        var uploaded = new DemoUploadedDto("Sprint 22", Guid.Parse("00000000-0000-4000-8000-0000000000a1"), 50841, 3, "https://shiplean.example/p/SHIP/items/SHIP-9");
        _fakes.Uploads.Upload.Returns(uploaded);

        using var response = await SendFileAsync(HttpMethod.Post, "api/uploads/demo?token=live-token");

        var expected = new JsonObject
        {
            ["sprintName"] = "Sprint 22",
            ["videoAttachmentId"] = "00000000-0000-4000-8000-0000000000a1",
            ["videoBytes"] = 50841,
            ["chapters"] = 3,
            ["openUrl"] = "https://shiplean.example/p/SHIP/items/SHIP-9",
        };
        JsonNode.DeepEquals(await BodyOfAsync(response), expected).ShouldBeTrue();
        _fakes.Uploads.LastToken.ShouldBe("live-token");
    }

    [Test]
    public async Task Upload_WhenRefused_ShouldNameThePart()
    {
        _fakes.Uploads.Upload.Throws(ApiException.Validation("The video must be an MP4."));

        using var response = await SendFileAsync(HttpMethod.Post, "api/uploads/demo?token=live-token");

        var error = await ErrorOfAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Error.ShouldContain("video");
    }

    [Test]
    public async Task Upload_WhenTokenDead_ShouldBeNotFound()
    {
        _fakes.Uploads.Upload.Throws(ApiException.NotFound("That upload link is not valid."));

        using var response = await SendFileAsync(HttpMethod.Post, "api/uploads/demo?token=dead-token");

        var error = await ErrorOfAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Field.ShouldBe(null);
    }
}
