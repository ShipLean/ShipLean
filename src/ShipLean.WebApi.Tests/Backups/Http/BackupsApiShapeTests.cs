using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Features.Backups.Admin;
using ShipLean.Contracts.Backups.Dtos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Backups;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Backups.Http;

// The wire shapes and error mapping of the five routes, with FakeBackupAdmin answering the pinned examples. The run
// answer is per test (_runNow), since one host serves the whole class.
[TestFixture]
public class BackupsApiShapeTests : ApiFixture
{
    private const string BucketField = "destination.s3.bucket";

    private FakeBackupAdmin _admin = null!;
    private Func<BackupRunDto> _runNow = Accepted;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        _admin = new FakeBackupAdmin(
            get: () => FakeBackupAdmin.Pinned<BackupSettingsDto>("backup-settings.json", "configured"),
            save: body => string.IsNullOrEmpty(body.Destination.S3?.Bucket)
                ? throw ApiException.Validation("Enter the bucket name.", BucketField)
                : FakeBackupAdmin.Pinned<BackupSettingsDto>("backup-settings.json", "configured"),
            test: _ => FakeBackupAdmin.Pinned<BackupTestResultDto>("backup-test-result.json", "failed"),
            previewCron: cron => FakeBackupAdmin.Pinned<CronPreviewDto>("backup-cron-preview.json", cron == "0 2 * * *" ? "valid" : "invalid"),
            runNow: () => _runNow());
        services.RemoveAll<IBackupAdmin>();
        services.AddSingleton<IBackupAdmin>(_admin);
    };

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    private static BackupRunDto Accepted() => FakeBackupAdmin.Pinned<BackupRunDto>("backup-run.json", "running");

    private static JsonNode Pinned(string file, string example)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", file)))![example]!;

    private static JsonObject SaveBody(string bucket)
    {
        var body = Pinned("backup-save.json", "first").DeepClone().AsObject();
        body["destination"]!["s3"]!["bucket"] = bucket;
        return body;
    }

    [SetUp]
    public void Reset()
    {
        _runNow = Accepted;
        Recorder.Clear();
    }

    [Test]
    public async Task Get_WhenAdmin_ShouldReturnPinnedSettings()
    {
        using var response = await Api.GetAsync("api/instance/backups");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), Pinned("backup-settings.json", "configured")).ShouldBeTrue(text);
    }

    [Test]
    public async Task Put_WhenBucketEmpty_ShouldReturnValidationWithField()
    {
        using var response = await Api.PutJsonAsync("api/instance/backups", SaveBody(""));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe(BucketField);
        _admin.Saves.ShouldContain(s => s.Body.Destination.S3!.Bucket == "" && s.UserId == SeedIds.Users.Joel);
    }

    [Test]
    public async Task Put_WhenSaved_ShouldTrackSettingsSaved()
    {
        using var response = await Api.PutJsonAsync("api/instance/backups", Pinned("backup-save.json", "first"));

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), Pinned("backup-settings.json", "configured")).ShouldBeTrue(text);
        _admin.Saves.ShouldContain(s => s.Body.Passphrase == "correct horse battery staple" && s.UserId == SeedIds.Users.Joel);
        var tracked = Recorder.Events.Where(e => e.Source == "api:Backups.Put").ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.BackupSettingsSaved);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.Actor.ShouldBe(UsageActor.ForUser(SeedIds.Users.Joel));
        tracked.Outcome.ShouldBe(UsageOutcome.Ok);
    }

    [Test]
    public async Task RunNow_WhenConflict_ShouldReturn409()
    {
        _runNow = () => throw ApiException.Conflict("A backup is already running.");

        using var response = await Api.PostJsonAsync("api/instance/backups/run");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict);
        error.Code.ShouldBe(ApiErrorCodes.Conflict);
        error.Error.ShouldBe("A backup is already running.");
    }

    [Test]
    public async Task RunNow_WhenAccepted_ShouldReturn202WithRun()
    {
        using var response = await Api.PostJsonAsync("api/instance/backups/run");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), Pinned("backup-run.json", "running")).ShouldBeTrue(text);
        _admin.Runs.ShouldContain(SeedIds.Users.Joel);
        Recorder.Events.Where(e => e.Source == "api:Backups.Run").ShouldHaveSingleItem().Name.ShouldBe(UsageEventNames.BackupRunStarted);
    }

    [Test]
    public async Task RunNow_WhenNotConfigured_ShouldReturn503()
    {
        _runNow = () => throw ApiException.NotConfigured("Set up where backups go first.");

        using var response = await Api.PostJsonAsync("api/instance/backups/run");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.ServiceUnavailable);
        error.Code.ShouldBe(ApiErrorCodes.NotConfigured);
    }

    [Test]
    public async Task CronPreview_WhenInvalid_ShouldReturnValidFalse()
    {
        using var response = await Api.PostJsonAsync("api/instance/backups/cron-preview", new JsonObject { ["cron"] = "every day" });

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), Pinned("backup-cron-preview.json", "invalid")).ShouldBeTrue(text);
        _admin.Previews.ShouldContain("every day");
        Recorder.Events.ShouldNotContain(e => e.Source == "api:Backups.CronPreview");
    }

    [Test]
    public async Task Test_WhenProbeFails_ShouldReturnOkFalse()
    {
        using var response = await Api.PostJsonAsync("api/instance/backups/test", Pinned("backup-save.json", "keepSecrets")["destination"]!.DeepClone());

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.DeepEquals(JsonNode.Parse(text), Pinned("backup-test-result.json", "failed")).ShouldBeTrue(text);
        _admin.Tests.ShouldContain(d => d.S3!.Region == "ap-southeast-1" && d.S3.SecretKey == null);
        Recorder.Events.Where(e => e.Source == "api:Backups.Test").ShouldHaveSingleItem().Name.ShouldBe(UsageEventNames.BackupConnectionTested);
    }
}
