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
    private readonly List<FreshDatabase> _databases = [];
    private readonly List<IAsyncDisposable> _owned = [];

    [TearDown]
    public async Task DropDatabases()
    {
        foreach (var owned in _owned)
        {
            await owned.DisposeAsync();
        }

        foreach (var database in _databases)
        {
            await database.DisposeAsync();
        }

        _owned.Clear();
        _databases.Clear();
    }

    protected override (IBlobStore Store, IBlobInventory Inventory) Create()
    {
        var database = FreshDatabase.CreateAsync(includeSeed: false).GetAwaiter().GetResult();
        _databases.Add(database);
        var pinned = DatabaseServices.PinUtc(database.ConnectionString);
        PostgresBlobStore.ApplySchema(pinned);
        var store = new PostgresBlobStore(pinned);
        var inventory = new PostgresBlobInventory(pinned);
        _owned.Add(store);
        _owned.Add(inventory);
        return (store, inventory);
    }
}
