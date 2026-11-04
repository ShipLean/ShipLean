using System.Text;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Storage;
using ShipLean.LocalEnvironment.Database;

namespace ShipLean.WebApi.Tests.Storage;

// What the contract can't see: the Postgres store holds a pooled connection for as long as a ranged stream is open.
[TestFixture]
public class PostgresBlobStoreRangeTests
{
    private const int Cycles = 20;

    private FreshDatabase _database = null!;

    [OneTimeSetUp]
    public async Task CreateDatabase()
    {
        _database = await FreshDatabase.CreateAsync(includeSeed: false);
        PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(_database.ConnectionString));
    }

    [OneTimeTearDown]
    public async Task DropDatabase() => await _database.DisposeAsync();

    [Test]
    public async Task OpenAt_WhenOpenedAndDisposedManyTimes_ShouldNotExhaustThePool()
    {
        // Two connections and a two second wait for one: a stream that keeps its connection fails the third open.
        var small = new NpgsqlConnectionStringBuilder(DatabaseServices.PinUtc(_database.ConnectionString)) { MaxPoolSize = 2, Timeout = 2, Pooling = true };
        await using var store = new PostgresBlobStore(small.ConnectionString);
        await store.PutAsync("video/a.bin", new MemoryStream(Encoding.ASCII.GetBytes("0123456789")), "video/mp4", CancellationToken.None);

        for (var i = 0; i < Cycles; i++)
        {
            await using (var rest = await store.OpenAtAsync("video/a.bin", i % 10, CancellationToken.None))
            {
                (await rest!.ReadAsync(new byte[1])).ShouldBe(1);
            }

            (await store.OpenAtAsync("video/none.bin", 0, CancellationToken.None)).ShouldBeNull();
        }

        await using var last = await store.OpenAtAsync("video/a.bin", 8, CancellationToken.None);
        (await new StreamReader(last!).ReadToEndAsync()).ShouldBe("89");
    }
}
