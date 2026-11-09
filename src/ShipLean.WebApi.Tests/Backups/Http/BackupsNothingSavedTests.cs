using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Backups.Admin;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backups.Http;

// The routes against the production registration, BackupAdmin, on an instance where backups were never saved: GET is
// the pinned not-configured settings, the cron preview and the connection test answer, Run now is not_configured, and
// the first save configures the instance (last, as it changes the scenario).
[TestFixture]
public class BackupsNothingSavedTests : ApiFixture
{
    // A closed port on loopback: the probe fails at once, whichever destination factory is registered.
    private const string UnreachableDestination = """
        {"provider":"s3","s3":{"region":"us-east-1","endpointUrl":"http://127.0.0.1:9","bucket":"shiplean-backups","prefix":"shiplean/","forcePathStyle":true,"accessKeyId":null,"secretKey":null}}
        """;

    // The pinned labels and next runs are computed at this instant (PinnedShapes/README.md).
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 14, 0, 0, TimeSpan.Zero)));

    private static JsonNode Pinned(string file, string example)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", file)))![example]!;

    [Test]
    [Order(1)]
    public async Task Get_WhenNothingSaved_ShouldReturnNotConfiguredShape()
    {
        Host.Services.CreateScope().ServiceProvider.GetRequiredService<IBackupAdmin>().ShouldBeOfType<BackupAdmin>();

        using var response = await Api.GetAsync("api/instance/backups");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), Pinned("backup-settings.json", "notConfigured")).ShouldBeTrue(text);
    }

    [Test]
    [Order(2)]
    public async Task Route_WhenNothingSaved_ShouldReturnNotConfigured()
    {
        using var response = await BackupRoutes.SendAsync(Api, "POST", "api/instance/backups/run", null);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.ServiceUnavailable);
        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
    }

    [Test]
    [Order(3)]
    public async Task CronPreview_WhenNothingSaved_ShouldReturnPinnedPreview()
    {
        using var response = await BackupRoutes.SendAsync(Api, "POST", "api/instance/backups/cron-preview", BackupRoutes.CronBody);

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), Pinned("backup-cron-preview.json", "valid")).ShouldBeTrue(text);
    }

    [Test]
    [Order(4)]
    public async Task Test_WhenNothingSaved_ShouldReturnOkFalse()
    {
        using var response = await BackupRoutes.SendAsync(Api, "POST", "api/instance/backups/test", UnreachableDestination);

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var result = JsonNode.Parse(text)!;
        result["ok"]!.GetValue<bool>().ShouldBeFalse(text);
        result["message"]!.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }

    [Test]
    [Order(5)]
    public async Task Put_WhenNothingSaved_ShouldConfigure()
    {
        using var response = await BackupRoutes.SendAsync(Api, "PUT", "api/instance/backups", BackupRoutes.SaveBody);

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var settings = JsonNode.Parse(text)!;
        settings["health"]!.GetValue<string>().ShouldBe("neverRun");
        settings["passphraseGeneration"]!.GetValue<int>().ShouldBe(1);
    }
}
