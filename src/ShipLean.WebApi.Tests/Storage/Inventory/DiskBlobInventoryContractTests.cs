using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Storage;
using ShipLean.Application.Storage.Inventory;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Storage.Inventory;

// BlobInventoryContract on a folder: one temporary folder per test.
[TestFixture]
public class DiskBlobInventoryContractTests : BlobInventoryContract
{
    private string _root = null!;

    [SetUp]
    public void MakeRoot() => _root = Path.Combine(Path.GetTempPath(), $"shiplean-inventory-{Guid.NewGuid():N}");

    [TearDown]
    public void RemoveRoot() => Directory.Delete(_root, recursive: true);

    protected override (IBlobStore Store, IBlobInventory Inventory) Create()
        => (new LocalDiskBlobStore(_root), new LocalDiskBlobInventory(_root));

    [Test]
    public async Task ListKeys_WhenTempFileLeft_ShouldSkipIt()
    {
        var (store, inventory) = Create();
        await store.PutAsync("attachments/kept", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken.None);
        await File.WriteAllBytesAsync(Path.Combine(_root, "attachments", $"kept.{Guid.NewGuid():N}{BlobKey.TempSuffix}"), "x"u8.ToArray());

        var listed = await inventory.ListKeysAsync(CancellationToken.None).ToListAsync();

        listed.ShouldBe(["attachments/kept"]);
    }
}
