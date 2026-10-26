using ShipLean.Application.Database;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport.Contracts.Blobs;
using ShipLean.TestSupport.Database;

namespace ShipLean.WebApi.Tests.Storage;

// BlobStoreContract on Postgres rows: one database for the class with db/blobs applied, one store per test.
[TestFixture]
public class PostgresBlobStoreContractTests : BlobStoreContract
{
    private FreshDatabase _database = null!;
    private readonly List<PostgresBlobStore> _stores = [];

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        _database = await FreshDatabase.CreateAsync(includeSeed: false);
        PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(_database.ConnectionString));
    }

    [OneTimeTearDown]
    public async Task DropDatabase()
    {
        foreach (var store in _stores)
        {
            await store.DisposeAsync();
        }
        await _database.DisposeAsync();
    }

    protected override IBlobStore Create()
    {
        var store = new PostgresBlobStore(DatabaseServices.PinUtc(_database.ConnectionString));
        _stores.Add(store);
        return store;
    }
}
