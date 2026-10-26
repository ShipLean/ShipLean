using Dapper;
using ShipLean.Application.Storage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

namespace ShipLean.WebApi.Tests.Storage;

// The real app with SHIPLEAN_BLOB_DB_CONNECTION pointing at the test database: an uploaded attachment downloads whole
// and its bytes are a row of the blob table (the host applies db/blobs at startup).
[TestFixture]
public class PostgresBlobStoreApiTests
{
    [Test]
    public async Task Upload_WhenBlobStoreIsPostgres_ShouldDownloadSameBytesFromBlobTable()
    {
        await using var database = await FreshDatabase.CreateAsync();
        await using var host = ShipLeanHost.Start(
            database.ConnectionString,
            TestUser.Joel,
            settings: new Dictionary<string, string?> { [BlobStoreServices.DbConnectionKey] = database.ConnectionString });
        using var client = host.CreateClient();
        var api = new ApiClient(client);
        var bytes = Enumerable.Range(0, 5000).Select(i => (byte)i).ToArray();

        var added = await AttachmentUploads.UploadAsync(client, $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments", "data.bin", "application/octet-stream", bytes);
        using var download = await api.GetAsync(added.DownloadUrl);

        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        await using var connection = await database.OpenAsync();
        (await connection.ExecuteScalarAsync<long>("SELECT coalesce(sum(size_bytes), 0) FROM blob")).ShouldBe(bytes.Length);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM blob WHERE octet_length(bytes) = size_bytes")).ShouldBe(1);
    }
}
