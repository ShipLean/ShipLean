using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Admin;
using ShipLean.Contracts.Errors;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backups.Http;

// The routes against the production registration, PendingBackupAdmin, until BackupAdmin replaces it. When it does,
// Get_WhenPendingAdmin_ShouldReturnNotConfiguredShape's expected value becomes the real not-configured settings.
[TestFixture]
public class BackupsPendingAdminTests : ApiFixture
{
    private static JsonNode PinnedNotConfigured()
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", "backup-settings.json")))!["notConfigured"]!;

    [Test]
    public async Task Get_WhenPendingAdmin_ShouldReturnNotConfiguredShape()
    {
        Host.Services.CreateScope().ServiceProvider.GetRequiredService<IBackupAdmin>().ShouldBeOfType<PendingBackupAdmin>();
        var before = TimeProvider.System.GetUtcNow();

        using var response = await Api.GetAsync("api/instance/backups");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var body = JsonNode.Parse(text)!.AsObject();
        var runs = body["schedule"]!["nextRuns"]!.AsArray();
        runs.Count.ShouldBe(5);
        var first = DateTimeOffset.Parse(runs[0]!["at"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture);
        first.ShouldBeGreaterThan(before);
        (first - before).ShouldBeLessThanOrEqualTo(TimeSpan.FromDays(1));
        for (var i = 0; i < runs.Count; i++)
        {
            var at = first.AddDays(i);
            at.TimeOfDay.ShouldBe(TimeSpan.FromHours(2));
            runs[i]!["at"]!.GetValue<string>().ShouldBe(at.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
            runs[i]!["label"]!.GetValue<string>().ShouldBe(DisplayDate.Format(at));
        }
        var expected = PinnedNotConfigured().DeepClone().AsObject();
        expected["schedule"]!["nextRuns"] = runs.DeepClone();
        JsonNode.DeepEquals(body, expected).ShouldBeTrue(text);
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
