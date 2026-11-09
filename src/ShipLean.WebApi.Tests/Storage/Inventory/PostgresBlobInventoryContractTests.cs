using ShipLean.Application.Database;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Storage;
using ShipLean.Application.Storage.Inventory;
using ShipLean.Contracts.Blobs;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Storage.Inventory;

// BlobInventoryContract on Postgres rows: a fresh database with db/blobs applied for each test, so "empty" is empty.
[TestFixture]
public class PostgresBlobInventoryContractTests : BlobInventoryContract
{
    private FreshDatabase _database = null!;
    private PostgresBlobStore _store = null!;
    private PostgresBlobInventory _inventory = null!;

    [SetUp]
    public async Task CreateDatabase()
    {
        _database = await FreshDatabase.CreateAsync(includeSeed: false);
        var pinned = DatabaseServices.PinUtc(_database.ConnectionString);
        PostgresBlobStore.ApplySchema(pinned);
        _store = new PostgresBlobStore(pinned);
        _inventory = new PostgresBlobInventory(pinned);
    }

    [TearDown]
    public async Task DropDatabase()
    {
        await _inventory.DisposeAsync();
        await _store.DisposeAsync();
        await _database.DisposeAsync();
    }

    protected override (IBlobStore Store, IBlobInventory Inventory) Create() => (_store, _inventory);

    [Test]
    public async Task ListKeys_WhenCancelledMidway_ShouldThrow()
    {
        await _store.PutAsync("attachments/a", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken.None);
        await _store.PutAsync("attachments/b", new MemoryStream("x"u8.ToArray()), "text/plain", CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await using var keys = _inventory.ListKeysAsync(cancellation.Token).GetAsyncEnumerator();
        (await keys.MoveNextAsync()).ShouldBeTrue();

        // Cancelled after the query is open, so only the per-row check can stop it.
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(async () => await keys.MoveNextAsync());
    }
}
