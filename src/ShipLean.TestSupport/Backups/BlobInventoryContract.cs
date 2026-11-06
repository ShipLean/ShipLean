using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Blobs;
using Shouldly;

namespace ShipLean.TestSupport.Backups;

// Behaviour every IBlobInventory must have: it lists the keys of the store it was made over. The fixture stores blobs
// through the IBlobStore that Create returns, so a real inventory is checked against its own store.
[TestFixture]
public abstract class BlobInventoryContract
{
    protected abstract (IBlobStore Store, IBlobInventory Inventory) Create();

    [Test]
    public async Task ListKeys_WhenBlobsStored_ShouldReturnEveryKey()
    {
        var (store, inventory) = Create();
        string[] keys = ["attachments/9f1c2d3e", "attachments/0a0b0c0d", "covers/a.png", "photos/deep/er/b.jpg"];
        foreach (var key in keys)
        {
            await store.PutAsync(key, new MemoryStream("x"u8.ToArray()), "application/octet-stream", CancellationToken.None);
        }

        var listed = await inventory.ListKeysAsync(CancellationToken.None).ToListAsync();

        listed.ShouldBe(keys, ignoreOrder: true);
    }

    [Test]
    public async Task ListKeys_WhenEmpty_ShouldReturnNothing()
    {
        var (_, inventory) = Create();

        var listed = await inventory.ListKeysAsync(CancellationToken.None).ToListAsync();

        listed.ShouldBeEmpty();
    }
}
