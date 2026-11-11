using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Blobs;
using Shouldly;

namespace ShipLean.TestSupport.Backups;

// Behaviour every IBlobInventory must have: it lists the keys of the store it was made over, and never a temporary file
// of that store. A subclass supplies the real store and the inventory over it through Create, so each inventory is
// checked against its own store's way of writing.
[TestFixture]
public abstract class BlobInventoryContract
{
    protected abstract (IBlobStore Store, IBlobInventory Inventory) Create();

    [Test]
    public async Task ListKeys_WhenBlobsStored_ShouldReturnEveryKey()
    {
        var (store, inventory) = Create();
        string[] keys = ["attachments/9f1c2d3e", "attachments/0a0b0c0d", "covers/a.png", "photos/deep/er/b.jpg", "notes/caf\u00e9 v2+%.txt"];
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

    // A put that has written some bytes and not finished: a folder store has a temporary file by now, and a database
    // or S3 store has a partial upload. None of that is a key.
    [Test]
    public async Task ListKeys_WhenPutInFlight_ShouldNotListTempFiles()
    {
        var (store, inventory) = Create();
        await store.PutAsync("attachments/stored", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken.None);
        using var gated = new GatedStream();
        var put = store.PutAsync("attachments/inflight", gated, "text/plain", CancellationToken.None);
        List<string> during;
        try
        {
            await gated.Paused;
            during = await inventory.ListKeysAsync(CancellationToken.None).ToListAsync();
        }
        finally
        {
            gated.Release();
            await put;
        }

        during.ShouldNotContain(key => key.EndsWith(BlobKey.TempSuffix, StringComparison.Ordinal));
        during.ShouldNotContain("attachments/inflight");
        during.ShouldContain("attachments/stored");
        var after = await inventory.ListKeysAsync(CancellationToken.None).ToListAsync();
        after.ShouldBe(["attachments/stored", "attachments/inflight"], ignoreOrder: true);
    }
}
