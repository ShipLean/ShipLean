using Npgsql;
using ShipLean.Application.Features.Demos.Brand;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Demos;

// K18's logo row is read as one snapshot: the version in the URL, the blob opened and the type reported must all be the row's as first read,
// even when another request replaces the logo in between. The data source double replaces the logo as the store opens a second connection.
[TestFixture]
public class DemoLogoStoreTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    [Test]
    public async Task ReadAsync_WhenLogoReplacedBetweenSteps_ShouldKeepVersionBlobAndTypeFromTheFirstRow()
    {
        var blobs = new TypedBlobs(new() { ["demo-logos/old"] = "image/png", ["demo-logos/new"] = "image/jpeg" });
        await using var plain = NpgsqlDataSource.Create(ConnectionString);
        var plainStore = new DemoLogoStore(plain, blobs, TimeProvider.System);
        await plainStore.SetAsync(Project, "demo-logos/old", SeedIds.Users.Mei);

        var opens = 0;
        var builder = new NpgsqlDataSourceBuilder(new NpgsqlConnectionStringBuilder(ConnectionString) { Pooling = false }.ConnectionString);
        builder.UsePhysicalConnectionInitializer(_ => { }, async _ =>
        {
            if (Interlocked.Increment(ref opens) == 2)
            {
                await plainStore.SetAsync(Project, "demo-logos/new", SeedIds.Users.Mei);
            }
        });
        await using var racing = builder.Build();

        var read = await new DemoLogoStore(racing, blobs, TimeProvider.System).ReadAsync(Project);

        read.ShouldNotBeNull();
        read.Url.ShouldEndWith("?v=1");
        read.ContentType.ShouldBe("image/png");
        blobs.Opened().ShouldBe("demo-logos/old");
    }

    [Test]
    public async Task ReadAsync_WhenStoredTypeIsNotAnImage_ShouldBeNull()
    {
        var blobs = new TypedBlobs(new() { ["demo-logos/html"] = "text/html" });
        await using var plain = NpgsqlDataSource.Create(ConnectionString);
        var store = new DemoLogoStore(plain, blobs, TimeProvider.System);
        await store.SetAsync(Project, "demo-logos/html", SeedIds.Users.Mei);

        (await store.ReadAsync(Project)).ShouldBeNull();
    }

    private sealed class TypedBlobs(Dictionary<string, string> types) : IBlobStore
    {
        private readonly List<string> _opened = [];

        public string Opened() => string.Join(",", _opened);

        public Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken)
        {
            _opened.Add(key);
            return Task.FromResult<BlobContent?>(types.TryGetValue(key, out var type) ? new BlobContent(new MemoryStream([1, 2, 3]), type) : null);
        }

        public Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Stream?> OpenAtAsync(string key, long offset, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) => Task.FromResult(types.ContainsKey(key));
    }
}
