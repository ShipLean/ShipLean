using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Amazon.S3;
using ShipLean.Application.Storage;
using Amazon.S3.Model;
using ShipLean.Contracts.Backups.Dtos;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Storage;
using ShipLean.WebApi.Tests.WorkItemDetails.Attachments;
using ShipLean.Contracts.Attachments;

namespace ShipLean.WebApi.Tests.Backups.EndToEnd;

// The real app on a database of its own, with a folder as its blob store and a RustFS bucket as the backup destination
// (SHIP-801). Everything a backup needs is real: pg_dump, the SLBK1 cipher, the S3 destination, the mirror, the job and
// the worker. Tests drive it only through the HTTP routes an admin uses, and read the bucket with an S3 client of their
// own. Derived classes are one scenario each.
public abstract class BackupRoundTripFixture : ApiFixture
{
    protected const string BackupPrefix = "backups/e2e/";

    private static readonly TimeSpan RunLimit = TimeSpan.FromSeconds(120);

    // The wire format: enums cross as camelCase strings ("s3", "succeeded"), which JsonSerializerOptions.Web alone can't read or write.
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerOptions.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private S3BlobConnection _bucket = null!;
    private AmazonS3Client _s3 = null!;
    private string _scratch = null!;

    // The folder the app keeps its attachments in (SHIPLEAN_BLOB_ROOT).
    protected string BlobRoot { get; private set; } = null!;

    // An empty folder of the test's own, for downloads and restore targets.
    protected string Scratch => _scratch;

    protected AmazonS3Client S3 => _s3;

    protected string Bucket => _bucket.Bucket;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { ["SHIPLEAN_BLOB_ROOT"] = BlobRoot };

    protected override async Task BeforeHostStartAsync()
    {
        _scratch = Path.Combine(Path.GetTempPath(), $"shiplean-e2e-{Guid.NewGuid():N}");
        BlobRoot = Path.Combine(_scratch, "blobs");
        Directory.CreateDirectory(BlobRoot);
        _bucket = await S3Server.NewBucketAsync();
        _s3 = S3Server.CreateClient(_bucket);
    }

    protected override Task AfterHostStopAsync()
    {
        _s3?.Dispose();
        if (_scratch is not null)
        {
            Directory.Delete(_scratch, recursive: true);
        }
        return Task.CompletedTask;
    }

    // PUT /api/instance/backups: the RustFS bucket as the destination, with the secret key and passphrase given.
    protected async Task<BackupSettingsDto> SaveAsync(string passphrase, int keepCount = 14, bool includeAttachments = true)
    {
        var destination = new S3DestinationSaveDto("us-east-1", _bucket.ServiceUrl!.ToString(), _bucket.Bucket, BackupPrefix, true, _bucket.AccessKey, _bucket.SecretKey);
        using var response = await Api.PutJsonAsync(
            "api/instance/backups",
            JsonSerializer.SerializeToNode(new BackupSaveDto(true, new BackupDestinationSaveDto(BackupProvider.S3, destination), "0 2 * * *", keepCount, includeAttachments, passphrase), Wire));
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<BackupSettingsDto>(text, Wire)!;
    }

    // POST run, then GET until the run is no longer running. A run turns succeeded before the job prunes and releases the
    // lock, so a POST straight after one can still answer 409: that one is asked again. Each wait is a round trip to the
    // app, never a sleep, and the whole run is bounded by RunLimit.
    protected async Task<BackupRunDto> RunNowAsync()
    {
        using var limit = new CancellationTokenSource(RunLimit);
        var started = await StartAsync(limit.Token);
        while (!limit.IsCancellationRequested)
        {
            var settings = await GetSettingsAsync();
            var run = settings.Runs.Single(r => r.Id == started.Id);
            if (run.State != BackupRunState.Running)
            {
                return run;
            }
        }
        throw new TimeoutException($"Backup run {started.Id} was still running after {RunLimit}.");
    }

    private async Task<BackupRunDto> StartAsync(CancellationToken limit)
    {
        while (!limit.IsCancellationRequested)
        {
            using var response = await Api.PostJsonAsync("api/instance/backups/run");
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                (await ApiClient.ReadApiErrorAsync(response)).Code.ShouldBe(ApiErrorCodes.Conflict);
                continue;
            }
            var text = await response.Content.ReadAsStringAsync(limit);
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted, text);
            return JsonSerializer.Deserialize<BackupRunDto>(text, Wire)!;
        }
        throw new TimeoutException($"A backup could not be started within {RunLimit}.");
    }

    // The same wait, for something the job does after it marks the run succeeded (the prune): polls the settings until
    // the predicate holds.
    protected async Task<BackupSettingsDto> SettingsWhereAsync(Func<BackupSettingsDto, bool> predicate)
    {
        using var limit = new CancellationTokenSource(RunLimit);
        while (!limit.IsCancellationRequested)
        {
            var settings = await GetSettingsAsync();
            if (predicate(settings))
            {
                return settings;
            }
        }
        throw new TimeoutException($"The backup settings did not reach the expected state within {RunLimit}.");
    }

    // GET /api/instance/backups, read with the wire format.
    protected async Task<BackupSettingsDto> GetSettingsAsync()
    {
        using var response = await Api.GetAsync("api/instance/backups");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<BackupSettingsDto>(text, Wire)!;
    }

    // The same wait, on the bucket: the job marks old runs pruned before it deletes their objects, so a listing taken as
    // soon as the history says pruned can still hold them. Lists until the keys under prefix are exactly expected.
    protected async Task<IReadOnlyList<string>> KeysWhenAsync(string prefix, IReadOnlyList<string> expected)
    {
        using var limit = new CancellationTokenSource(RunLimit);
        IReadOnlyList<string> keys = [];
        while (!limit.IsCancellationRequested)
        {
            keys = [.. (await ListAsync(prefix)).Select(o => o.Key).Order(StringComparer.Ordinal)];
            if (keys.SequenceEqual(expected.Order(StringComparer.Ordinal)))
            {
                return keys;
            }
        }
        throw new TimeoutException($"The keys under {prefix} were still [{string.Join(", ", keys)}] after {RunLimit}, not [{string.Join(", ", expected)}].");
    }

    // Every object under a bucket prefix, as the AWS CLI would list them.
    protected async Task<IReadOnlyList<S3Object>> ListAsync(string prefix)
    {
        var objects = new List<S3Object>();
        ListObjectsV2Response page;
        string? token = null;
        do
        {
            page = await _s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket.Bucket, Prefix = prefix, ContinuationToken = token });
            objects.AddRange(page.S3Objects);
            token = page.NextContinuationToken;
        }
        while (page.IsTruncated == true);
        return objects;
    }

    // aws s3 cp: one object into a file under folder, named by its last path segment.
    protected async Task<string> DownloadAsync(string key, string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, key[(key.LastIndexOf('/') + 1)..]);
        using var response = await _s3.GetObjectAsync(_bucket.Bucket, key);
        await response.WriteResponseStreamToFileAsync(path, append: false, CancellationToken.None);
        return path;
    }

    // aws s3 sync: every object under a prefix into folder, keeping the part of its key below the prefix.
    protected async Task<string> SyncAsync(string prefix, string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (var item in await ListAsync(prefix))
        {
            var path = Path.Combine(folder, item.Key[prefix.Length..]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var response = await _s3.GetObjectAsync(_bucket.Bucket, item.Key);
            await response.WriteResponseStreamToFileAsync(path, append: false, CancellationToken.None);
        }
        return folder;
    }

    // docs/backups.md steps 2 to 5 for one dump: download it, decrypt it, pg_restore it into an empty database, sync the
    // attachments of the same generation, restore-blobs them into a second empty folder, then start an app on both.
    protected async Task<RestoredInstance> RestoreAsync(string dumpKey, int generation, string passphrase)
    {
        var work = Path.Combine(_scratch, $"restore-{Guid.NewGuid():N}");
        var dumpFile = await DownloadAsync(dumpKey, Path.Combine(work, "download"));
        await Slbk1Header.CheckAsync(dumpFile);
        var plainDump = Path.Combine(work, "shiplean.dump");
        var decrypt = await RestoreRunbook.DecryptAsync(dumpFile, plainDump, passphrase);
        decrypt.ExitCode.ShouldBe(0, decrypt.Error);

        var database = await RestoreRunbook.CreateEmptyDatabaseAsync(ConnectionString);
        try
        {
            var restore = await database.PgRestoreAsync(plainDump);
            restore.ExitCode.ShouldBe(0, restore.Error);

            var attachments = await SyncAsync($"{BackupPrefix}attachments/g{generation}/", Path.Combine(work, "attachments", $"g{generation}"));
            foreach (var file in Directory.EnumerateFiles(attachments, "*", SearchOption.AllDirectories))
            {
                await Slbk1Header.CheckAsync(file);
            }

            var blobs = Path.Combine(work, "blobs");
            Directory.CreateDirectory(blobs);
            var restoreBlobs = await RestoreRunbook.RestoreBlobsAsync(attachments, blobs, passphrase);
            restoreBlobs.ExitCode.ShouldBe(0, restoreBlobs.Error);

            var host = ShipLeanHost.Start(database.ConnectionString, TestUser.Joel, settings: new Dictionary<string, string?> { ["SHIPLEAN_BLOB_ROOT"] = blobs });
            return new RestoredInstance(database, blobs, host);
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    // Every file under a blob folder (never a .tmp), relative to it, with its bytes.
    protected static IReadOnlyDictionary<string, byte[]> BlobFiles(string root)
        => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".tmp", StringComparison.Ordinal))
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes);

    // The same relative paths, each with the same bytes.
    protected static void ShouldHoldSameBlobs(IReadOnlyDictionary<string, byte[]> actual, IReadOnlyDictionary<string, byte[]> expected)
    {
        actual.Keys.Order().ShouldBe(expected.Keys.Order());
        foreach (var (path, bytes) in expected)
        {
            actual[path].ShouldBe(bytes, path);
        }
    }

    // SELECT count(*) of a table, on any database.
    protected static async Task<long> CountAsync(Npgsql.NpgsqlConnection connection, string table)
        => await Dapper.SqlMapper.ExecuteScalarAsync<long>(connection, $"SELECT count(*) FROM {table}");

    // Two attachments with bytes that a text round trip would damage, on a seeded item.
    protected async Task<IReadOnlyList<(AttachmentDto Dto, byte[] Bytes)>> UploadAttachmentsAsync(string suffix = "")
    {
        var url = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";
        var binary = new byte[70_000];
        new Random(801).NextBytes(binary);
        var text = System.Text.Encoding.UTF8.GetBytes($"Backup notes {suffix}\né中文 and a long line\n");
        var added = new List<(AttachmentDto, byte[])>
        {
            (await AttachmentUploads.UploadAsync(Client, url, $"payload{suffix}.bin", "application/octet-stream", binary), binary),
            (await AttachmentUploads.UploadAsync(Client, url, $"notes{suffix}.txt", "text/plain", text), text),
        };
        return added;
    }

    // POST api/work-items: a new action under a seeded story. Returns its key.
    protected async Task<string> CreateItemAsync(string title)
    {
        using var response = await Api.PostJsonAsync("api/work-items", new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = "action",
            ["title"] = title,
            ["parentId"] = SeedIds.WorkItems.Ship104,
        });
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonNode.Parse(text)!["key"]!.GetValue<string>();
    }
}
