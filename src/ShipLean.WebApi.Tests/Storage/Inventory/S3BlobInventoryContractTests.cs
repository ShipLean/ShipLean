using Amazon.S3.Model;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Storage;
using ShipLean.Application.Storage.Inventory;
using ShipLean.Contracts.Blobs;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Backups;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Storage;

namespace ShipLean.WebApi.Tests.Storage.Inventory;

// BlobInventoryContract on S3Server: a new bucket per test, so "empty" is empty.
[TestFixture]
public class S3BlobInventoryContractTests : BlobInventoryContract
{
    private S3BlobStore _store = null!;
    private S3BlobInventory _inventory = null!;

    [SetUp]
    public async Task CreateBucket()
    {
        var connection = await S3Server.NewBucketAsync();
        _store = new S3BlobStore(connection);
        _inventory = new S3BlobInventory(connection);
    }

    [TearDown]
    public void DisposeClients()
    {
        _store.Dispose();
        _inventory.Dispose();
    }

    protected override (IBlobStore Store, IBlobInventory Inventory) Create() => (_store, _inventory);

    [Test]
    public async Task ListKeys_WhenPrefixConfigured_ShouldStripIt()
    {
        var connection = await S3Server.NewBucketAsync("backups-test/");
        using var store = new S3BlobStore(connection);
        using var inventory = new S3BlobInventory(connection);
        await store.PutAsync("attachments/a", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken.None);
        using var client = S3Server.CreateClient(connection);
        await client.PutObjectAsync(new PutObjectRequest { BucketName = connection.Bucket, Key = "other/outside", ContentBody = "x" });

        var listed = await inventory.ListKeysAsync(CancellationToken.None).ToListAsync();

        listed.ShouldBe(["attachments/a"]);
    }

    [Test]
    public async Task ListKeys_WhenMoreThanOnePage_ShouldReturnEveryKey()
    {
        var (store, inventory) = Create();
        var keys = Enumerable.Range(0, 1100).Select(i => $"bulk/{i:D5}").ToArray();
        await Parallel.ForEachAsync(keys, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (key, ct) =>
            await store.PutAsync(key, new MemoryStream("x"u8.ToArray()), "text/plain", ct));

        var listed = await inventory.ListKeysAsync(CancellationToken.None).ToListAsync();

        listed.ShouldBe(keys, ignoreOrder: true);
    }

    [Test]
    public async Task Module_WhenS3Chosen_ShouldRegisterS3Inventory()
    {
        var connection = await S3Server.NewBucketAsync();
        await using var database = await ShipLeanDatabase.CloneSeedAsync();
        await using var host = ShipLeanHost.Start(
            database.ConnectionString,
            TestUser.Joel,
            settings: new Dictionary<string, string?> { [BlobStoreServices.S3ConnectionKey] = S3Server.ToSetting(connection) });

        var inventory = host.Services.GetRequiredService<IBlobInventory>();

        inventory.ShouldBeOfType<S3BlobInventory>();
    }
}
