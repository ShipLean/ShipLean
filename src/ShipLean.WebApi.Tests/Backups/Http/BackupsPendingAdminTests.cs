using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Backups.Admin;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backups.Http;

// The routes against the production registration, PendingBackupAdmin, until BackupAdmin replaces it. When it does,
// Get_WhenPendingAdmin_ShouldReturnNotConfiguredShape's expected value becomes the real not-configured settings.
[TestFixture]
public class BackupsPendingAdminTests : ApiFixture
{
    // The pinned labels and next runs are computed at this instant (PinnedShapes/README.md).
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 14, 0, 0, TimeSpan.Zero)));

    private static JsonNode PinnedNotConfigured()
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", "backup-settings.json")))!["notConfigured"]!;

    [Test]
    public async Task Get_WhenPendingAdmin_ShouldReturnNotConfiguredShape()
    {
        Host.Services.CreateScope().ServiceProvider.GetRequiredService<IBackupAdmin>().ShouldBeOfType<PendingBackupAdmin>();

        using var response = await Api.GetAsync("api/instance/backups");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), PinnedNotConfigured()).ShouldBeTrue(text);
    }

    [TestCase("PUT", "api/instance/backups", BackupRoutes.SaveBody)]
    [TestCase("POST", "api/instance/backups/test", BackupRoutes.DestinationBody)]
    [TestCase("POST", "api/instance/backups/cron-preview", BackupRoutes.CronBody)]
    [TestCase("POST", "api/instance/backups/run", null)]
    public async Task Route_WhenPendingAdmin_ShouldReturnNotConfigured(string method, string url, string? body)
    {
        using var response = await BackupRoutes.SendAsync(Api, method, url, body);

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.ServiceUnavailable);
        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
    }
}
