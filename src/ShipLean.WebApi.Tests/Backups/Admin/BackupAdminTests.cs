using System.Net;
using System.Text.Json.Nodes;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Backups.Admin;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups;
using ShipLean.Contracts.Errors;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Backups.Admin;

// BackupAdmin and BackupConfigReader over the real backup_settings row and run log. Each test gets its own database
// and host, because the settings row and the run log are one per instance.
[TestFixture]
public class BackupAdminTests
{
    private const string Passphrase = "correct horse battery staple";
    private const string Secret = "example-secret-not-a-real-key-7Hq2";
    private const string AccessKeyId = "SHIPLEANEXAMPLEKEY01";

    private static readonly DateTimeOffset Start = new(2026, 10, 24, 14, 0, 0, TimeSpan.Zero);

    private ClonedDatabase _database = null!;
    private ShipLeanHost _host = null!;
    private HttpClient _client = null!;
    private ApiClient _api = null!;
    private FakeTimeProvider _clock = null!;
    private RecordingDestinationFactory _destinations = null!;

    [SetUp]
    public async Task StartHost()
    {
        _database = await ShipLeanDatabase.CloneSeedAsync();
        StartHostWith(new Dictionary<string, string?>());
    }

    [TearDown]
    public async Task StopHost()
    {
        await StopHostOnlyAsync();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task Save_WhenFirstWithoutPassphrase_ShouldFailOnPassphrase()
    {
        var body = SaveBody();
        body["passphrase"] = null;

        var error = await SaveErrorAsync(body);

        error.Field.ShouldBe("passphrase");
        (await ReadRowAsync()).ShouldBeNull();
    }

    [Test]
    public async Task Save_WhenPassphraseChanged_ShouldIncrementGeneration()
    {
        (await SaveAsync(SaveBody()))["passphraseGeneration"]!.GetValue<int>().ShouldBe(1);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var keep = SaveBody();
        keep["passphrase"] = null;
        keep["destination"]!["s3"]!["secretKey"] = null;
        (await SaveAsync(keep))["passphraseGeneration"]!.GetValue<int>().ShouldBe(1);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var same = SaveBody();
        (await SaveAsync(same))["passphraseGeneration"]!.GetValue<int>().ShouldBe(1);
        _clock.Advance(TimeSpan.FromMinutes(5));
        var changed = SaveBody();
        changed["passphrase"] = "a brand new passphrase for g2";

        var settings = await SaveAsync(changed);

        settings["passphraseGeneration"]!.GetValue<int>().ShouldBe(2);
        settings["passphraseSetAt"]!.GetValue<string>().ShouldBe("2026-10-24T14:15:00Z");
        var config = (await ReadConfigAsync())!;
        config.Passphrase.ShouldBe("a brand new passphrase for g2");
        config.PassphraseGeneration.ShouldBe(2);
    }

    [Test]
    public async Task Save_WhenSecretNull_ShouldKeepSaved()
    {
        await SaveAsync(SaveBody());
        _clock.Advance(TimeSpan.FromMinutes(1));
        var keep = SaveBody();
        keep["destination"]!["s3"]!["secretKey"] = null;
        keep["destination"]!["s3"]!["bucket"] = "shiplean-backups-2";
        keep["passphrase"] = null;

        var settings = await SaveAsync(keep);

        settings["destination"]!["s3"]!["secretKeyLast4"]!.GetValue<string>().ShouldBe("7Hq2");
        settings["destination"]!["s3"]!["bucket"]!.GetValue<string>().ShouldBe("shiplean-backups-2");
        var s3 = (await ReadConfigAsync())!.Destination.S3!;
        s3.SecretKey.ShouldBe(Secret);
        s3.AccessKeyId.ShouldBe(AccessKeyId);
    }

    [Test]
    public async Task Save_WhenSecretEmpty_ShouldUseDefaultChain()
    {
        await SaveAsync(SaveBody());
        _clock.Advance(TimeSpan.FromMinutes(1));
        var stillKeyed = SaveBody();
        stillKeyed["destination"]!["s3"]!["secretKey"] = "";
        (await SaveErrorAsync(stillKeyed)).Field.ShouldBe("destination.s3.accessKeyId");
        var cleared = SaveBody();
        cleared["destination"]!["s3"]!["secretKey"] = "";
        cleared["destination"]!["s3"]!["accessKeyId"] = null;

        var settings = await SaveAsync(cleared);

        settings["destination"]!["s3"]!["accessKeyId"].ShouldBeNull();
        settings["destination"]!["s3"]!["secretKeyLast4"].ShouldBeNull();
        var s3 = (await ReadConfigAsync())!.Destination.S3!;
        s3.AccessKeyId.ShouldBeNull();
        s3.SecretKey.ShouldBeNull();
        (await ReadRowAsync())!.DestinationSecret.ShouldBeNull();
    }

    [TestCase(null, "us-east-1x", Description = "Amazon S3 needs a region from the AWS list")]
    [TestCase("https://ceph.example.internal:7480", "", Description = "an endpoint needs a signing region")]
    [TestCase("https://ceph.example.internal:7480", "us east 1", Description = "a signing region is one word")]
    public async Task Save_WhenRegionAndEndpoint_ShouldFail(string? endpoint, string region)
    {
        var body = SaveBody();
        body["destination"]!["s3"]!["endpointUrl"] = endpoint;
        body["destination"]!["s3"]!["region"] = region;

        var error = await SaveErrorAsync(body);

        error.Field.ShouldBe("destination.s3.region");
        (await ReadRowAsync()).ShouldBeNull();
    }

    [TestCase("https://admin:hunter2-password@ceph.example.internal:7480")]
    [TestCase("https://ceph.example.internal:7480/?token=hunter2-password")]
    [TestCase("ftp://ceph.example.internal/hunter2-password")]
    [TestCase("ceph.example.internal/hunter2-password")]
    public async Task Save_WhenEndpointHasUserInfo_ShouldFail(string endpoint)
    {
        var body = SaveBody();
        body["destination"]!["s3"]!["endpointUrl"] = endpoint;

        var error = await SaveErrorAsync(body);

        error.Field.ShouldBe("destination.s3.endpointUrl");
        error.Error.ShouldNotContain("hunter2");
        (await ReadRowAsync()).ShouldBeNull();
    }

    [Test]
    public async Task Save_WhenCronTooOften_ShouldFailOnCron()
    {
        var body = SaveBody();
        body["cron"] = "*/30 * * * *";

        var error = await SaveErrorAsync(body);

        error.Field.ShouldBe("cron");
        error.Error.ShouldBe("Runs must be at least 1 hour apart.");
        (await ReadRowAsync()).ShouldBeNull();
    }

    [TestCase("cron", "cron")]
    [TestCase("destination", "destination")]
    [TestCase("destination.s3", "destination.s3")]
    [TestCase("destination.s3.bucket", "destination.s3.bucket")]
    [TestCase("destination.s3.region", "destination.s3.region")]
    public async Task Save_WhenFieldNull_ShouldFailOnItsPath(string path, string field)
    {
        var body = SaveBody();
        var parts = path.Split('.');
        var parent = parts[..^1].Aggregate((JsonNode)body, (node, part) => node[part]!);
        parent[parts[^1]] = null;

        var error = await SaveErrorAsync(body);

        error.Field.ShouldBe(field);
    }

    [TestCase(0)]
    [TestCase(366)]
    public async Task Save_WhenKeepCountOutOfRange_ShouldFailOnKeepCount(int keepCount)
    {
        var body = SaveBody();
        body["keepCount"] = keepCount;

        (await SaveErrorAsync(body)).Field.ShouldBe("keepCount");
    }

    [TestCase("short pass")]
    [TestCase("            ")]
    public async Task Save_WhenPassphraseTooWeak_ShouldFailOnPassphrase(string passphrase)
    {
        var body = SaveBody();
        body["passphrase"] = passphrase;

        (await SaveErrorAsync(body)).Field.ShouldBe("passphrase");
    }

    [TestCase("Shiplean-Backups")]
    [TestCase("ab")]
    [TestCase("shiplean..backups")]
    [TestCase("192.168.1.10")]
    [TestCase("xn--backups")]
    [TestCase("backups-s3alias")]
    public async Task Save_WhenBucketBreaksS3Rules_ShouldFailOnBucket(string bucket)
    {
        var body = SaveBody();
        body["destination"]!["s3"]!["bucket"] = bucket;

        (await SaveErrorAsync(body)).Field.ShouldBe("destination.s3.bucket");
    }

    [TestCase("backups", "backups/")]
    [TestCase("shiplean/prod", "shiplean/prod/")]
    [TestCase("shiplean/", "shiplean/")]
    [TestCase("", "")]
    public async Task Save_WhenPrefixGiven_ShouldEndInSlash(string prefix, string expected)
    {
        var body = SaveBody();
        body["destination"]!["s3"]!["prefix"] = prefix;

        var settings = await SaveAsync(body);

        settings["destination"]!["s3"]!["prefix"]!.GetValue<string>().ShouldBe(expected);
        (await ReadConfigAsync())!.Destination.S3!.Prefix.ShouldBe(expected);
    }

    [Test]
    public async Task Get_WhenSaved_ShouldShowLastFourOnly()
    {
        await SaveAsync(SaveBody());

        using var response = await _api.GetAsync("api/instance/backups");

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var s3 = JsonNode.Parse(text)!["destination"]!["s3"]!;
        s3["secretKeyLast4"]!.GetValue<string>().ShouldBe("7Hq2");
        s3["accessKeyId"]!.GetValue<string>().ShouldBe(AccessKeyId);
        text.ShouldNotContain(Secret);
        text.ShouldNotContain(Passphrase);
        await using var connection = await _database.OpenAsync();
        var stored = await connection.QuerySingleAsync<(string Secret, string Passphrase, string Destination)>(
            "SELECT destination_secret, passphrase, destination::text FROM backup_settings");
        stored.Secret.ShouldNotContain(Secret);
        stored.Passphrase.ShouldNotContain(Passphrase);
        stored.Destination.ShouldNotContain(Secret);
    }

    [Test]
    public async Task Get_WhenLastRunFailed_ShouldBeFailing()
    {
        await SaveAsync(SaveBody());
        var log = _host.Services.GetRequiredService<IBackupRunLog>();
        var succeeded = await StartManualAsync(log);
        await using (succeeded)
        {
            (await log.SucceedAsync(succeeded.Run.Id, new BackupOutcome("shiplean/database/one.dump.slbk", 10, 0, 0))).ShouldBeTrue();
        }
        (await GetAsync())["health"]!.GetValue<string>().ShouldBe("ok");
        _clock.Advance(TimeSpan.FromMinutes(1));
        var failed = await StartManualAsync(log);
        await using (failed)
        {
            (await GetAsync())["health"]!.GetValue<string>().ShouldBe("running");
            _clock.Advance(TimeSpan.FromSeconds(9));
            (await log.FailAsync(failed.Run.Id, "The bucket refused the upload: Access Denied (403).")).ShouldBeTrue();
        }

        var settings = await GetAsync();

        settings["health"]!.GetValue<string>().ShouldBe("failing");
        var run = settings["runs"]![0]!;
        run["state"]!.GetValue<string>().ShouldBe("failed");
        run["error"]!.GetValue<string>().ShouldBe("The bucket refused the upload: Access Denied (403).");
        run["durationLabel"]!.GetValue<string>().ShouldBe("9s");
        run["startedBy"]!["id"]!.GetValue<Guid>().ShouldBe(SeedIds.Users.Joel);
        settings["lastSuccessAt"]!.GetValue<string>().ShouldBe("2026-10-24T14:00:00Z");
    }

    [Test]
    public async Task Get_WhenRunningRowHasNoLease_ShouldBeFailing()
    {
        await SaveAsync(SaveBody());
        await using (var connection = await _database.OpenAsync())
        {
            await connection.ExecuteAsync(
                "INSERT INTO backup_run (id, trigger, state, started_at, started_by, destination_id) VALUES (@id, 'manual', 'running', @at, @by, 'gone')",
                new { id = Guid.NewGuid(), at = Start, by = SeedIds.Users.Joel });
        }

        var settings = await GetAsync();

        settings["health"]!.GetValue<string>().ShouldBe("failing");
        settings["runs"]![0]!["state"]!.GetValue<string>().ShouldBe("running");
    }

    [Test]
    public async Task Get_WhenPastDue_ShouldBeOverdue()
    {
        await SaveAsync(SaveBody());
        (await GetAsync())["health"]!.GetValue<string>().ShouldBe("neverRun");
        using (var run = await _api.PostJsonAsync("api/instance/backups/run"))
        {
            run.StatusCode.ShouldBe(HttpStatusCode.Accepted, await run.Content.ReadAsStringAsync());
        }
        (await GetAsync())["health"]!.GetValue<string>().ShouldBe("ok");
        // The next occurrence is 2026-10-25 02:00; overdue an hour after it with no success since.
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 25, 2, 59, 59, TimeSpan.Zero));
        (await GetAsync())["health"]!.GetValue<string>().ShouldBe("ok");
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 25, 3, 0, 0, TimeSpan.Zero));

        (await GetAsync())["health"]!.GetValue<string>().ShouldBe("overdue");
    }

    [Test]
    public async Task Get_WhenBlobStoreSameDatabase_ShouldMarkInDatabaseBackup()
    {
        (await GetAsync())["attachmentStore"]!["inDatabaseBackup"]!.GetValue<bool>().ShouldBeFalse();
        await StopHostOnlyAsync();
        StartHostWith(new Dictionary<string, string?> { [BlobStoreServices.DbConnectionKey] = _database.ConnectionString });
        await SaveAsync(SaveBody());

        var store = (await GetAsync())["attachmentStore"]!;

        store["kind"]!.GetValue<string>().ShouldBe("postgres");
        store["inDatabaseBackup"]!.GetValue<bool>().ShouldBeTrue();
        (await ReadConfigAsync())!.AttachmentsInDatabase.ShouldBeTrue();
    }

    [Test]
    public async Task Save_WhenConcurrentPassphraseChanges_ShouldGiveDistinctGenerationsOrConflict()
    {
        await SaveAsync(SaveBody());
        _clock.Advance(TimeSpan.FromMinutes(1));

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(async i =>
        {
            var body = SaveBody();
            body["passphrase"] = $"concurrent passphrase number {i}";
            using var response = await _api.PutJsonAsync("api/instance/backups", body);
            return (response.StatusCode, Text: await response.Content.ReadAsStringAsync());
        }));

        var saved = responses.Where(r => r.StatusCode == HttpStatusCode.OK).Select(r => JsonNode.Parse(r.Text)!["passphraseGeneration"]!.GetValue<int>()).ToList();
        var conflicts = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        (saved.Count + conflicts.Count).ShouldBe(6, string.Join("\n", responses.Select(r => r.Text)));
        saved.ShouldNotBeEmpty();
        saved.ShouldBeUnique();
        conflicts.ShouldAllBe(r => r.Text.Contains(BackupAdmin.ConflictMessage));
        (await ReadRowAsync())!.PassphraseGeneration.ShouldBe(1 + saved.Count);
    }

    [Test]
    public async Task Test_WhenSecretNull_ShouldUseSaved()
    {
        await SaveAsync(SaveBody());
        var destination = SaveBody()["destination"]!.DeepClone();
        destination["s3"]!["secretKey"] = null;

        using var response = await _api.PostJsonAsync("api/instance/backups/test", destination);

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.Parse(text)!["ok"]!.GetValue<bool>().ShouldBeTrue(text);
        var s3 = _destinations.Configs.ShouldHaveSingleItem().S3!;
        s3.SecretKey.ShouldBe(Secret);
        s3.AccessKeyId.ShouldBe(AccessKeyId);
    }

    [Test]
    public async Task Test_WhenProbeFails_ShouldReturnTheFailedStep()
    {
        _destinations.Destination.Fail(BackupProbeResult.ReadStep);

        using var response = await _api.PostJsonAsync("api/instance/backups/test", SaveBody()["destination"]!.DeepClone());

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        var result = JsonNode.Parse(text)!;
        result["ok"]!.GetValue<bool>().ShouldBeFalse();
        result["step"]!.GetValue<string>().ShouldBe("read");
        _destinations.Configs.ShouldHaveSingleItem().S3!.SecretKey.ShouldBe(Secret);
    }

    [Test]
    public async Task CronPreview_WhenCronNull_ShouldBeInvalid()
    {
        using var response = await _api.PostJsonAsync("api/instance/backups/cron-preview", new JsonObject { ["cron"] = null });

        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        JsonNode.Parse(text)!["valid"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Test]
    public async Task Reader_WhenSaved_ShouldReturnDecryptedConfig()
    {
        (await ReadConfigAsync()).ShouldBeNull();
        await SaveAsync(SaveBody());

        var config = (await ReadConfigAsync())!;

        var expected = new BackupDestinationConfig("s3", new S3DestinationConfig(
            "us-east-1", "https://ceph.example.internal:7480", "shiplean-backups", "shiplean/", true, AccessKeyId, Secret));
        config.Destination.ShouldBe(expected);
        config.DestinationId.ShouldBe(BackupDestinationId.Of(expected));
        config.Passphrase.ShouldBe(Passphrase);
        config.PassphraseGeneration.ShouldBe(1);
        config.Enabled.ShouldBeTrue();
        config.Cron.ShouldBe("0 2 * * *");
        config.KeepCount.ShouldBe(14);
        config.IncludeAttachments.ShouldBeTrue();
        config.AttachmentsInDatabase.ShouldBeFalse();
        config.UpdatedAt.ShouldBe(Start);
    }

    private static JsonObject SaveBody()
        => JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", "backup-save.json")))!["first"]!
            .DeepClone().AsObject();

    private void StartHostWith(IReadOnlyDictionary<string, string?> settings)
    {
        _clock ??= new FakeTimeProvider(Start);
        _destinations ??= new RecordingDestinationFactory();
        _host = ShipLeanHost.Start(_database.ConnectionString, TestUser.Joel, services =>
        {
            services.AddSingleton<TimeProvider>(_clock);
            services.RemoveAll<IBackupDestinationFactory>();
            services.AddSingleton<IBackupDestinationFactory>(_destinations);
        }, settings: settings);
        _client = _host.CreateClient();
        _api = new ApiClient(_client);
        _host.ActAs(SeedIds.Users.Joel);
    }

    private async Task StopHostOnlyAsync()
    {
        _client.Dispose();
        await _host.DisposeAsync();
    }

    private async Task<JsonNode> SaveAsync(JsonNode body)
    {
        using var response = await _api.PutJsonAsync("api/instance/backups", body);
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonNode.Parse(text)!;
    }

    private async Task<ApiError> SaveErrorAsync(JsonNode body)
    {
        using var response = await _api.PutJsonAsync("api/instance/backups", body);
        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        return error;
    }

    private async Task<JsonNode> GetAsync()
    {
        using var response = await _api.GetAsync("api/instance/backups");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonNode.Parse(text)!;
    }

    private Task<BackupSettingsRow?> ReadRowAsync() => _host.Services.GetRequiredService<BackupSettingsRows>().ReadAsync();

    private Task<BackupConfig?> ReadConfigAsync() => _host.Services.GetRequiredService<IBackupConfigReader>().ReadAsync(CancellationToken.None);

    private static async Task<BackupLease> StartManualAsync(IBackupRunLog log)
    {
        var started = await log.TryStartAsync(new BackupStart(BackupTriggerKind.Manual, SeedIds.Users.Joel, null, 1, "destination"));
        return started.Lease ?? throw new InvalidOperationException($"The run was refused: {started.Refusal}.");
    }
}
