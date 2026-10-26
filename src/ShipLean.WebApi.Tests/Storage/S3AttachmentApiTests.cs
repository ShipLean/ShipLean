using System.Text;
using ShipLean.Application.Storage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Storage;
using ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

namespace ShipLean.WebApi.Tests.Storage;

// An attachment uploaded and downloaded through the real app whose blob store is S3 (set by SHIPLEAN_BLOB_S3_CONNECTION).
[TestFixture]
public class S3AttachmentApiTests
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private ClonedDatabase _database = null!;
    private ShipLeanHost _host = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task StartHost()
    {
        var connection = await S3Server.NewBucketAsync("attachments-test/");
        _database = await ShipLeanDatabase.CloneSeedAsync();
        _host = ShipLeanHost.Start(
            _database.ConnectionString,
            TestUser.Joel,
            settings: new Dictionary<string, string?> { [BlobStoreServices.S3ConnectionKey] = S3Server.ToSetting(connection) });
        _client = _host.CreateClient();
    }

    [OneTimeTearDown]
    public async Task StopHost()
    {
        _client.Dispose();
        await _host.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task Upload_WhenBlobStoreIsS3_ShouldDownloadTheSameBytes()
    {
        var bytes = Encoding.UTF8.GetBytes(new string('x', 5000));

        var added = await AttachmentUploads.UploadAsync(_client, ItemUrl, "notes.txt", "text/plain", bytes);
        using var download = await _client.GetAsync(added.DownloadUrl);

        download.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        download.Content.Headers.ContentLength.ShouldBe(bytes.Length);
    }
}
