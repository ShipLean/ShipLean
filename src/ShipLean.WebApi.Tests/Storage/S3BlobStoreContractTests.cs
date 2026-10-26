using ShipLean.Application.Storage;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport.Contracts.Blobs;
using ShipLean.TestSupport.Storage;

namespace ShipLean.WebApi.Tests.Storage;

// BlobStoreContract on S3Server: one bucket for the class, every test on its own keys.
[TestFixture]
public class S3BlobStoreContractTests : BlobStoreContract
{
    private S3BlobStore _store = null!;

    [OneTimeSetUp]
    public async Task CreateStore() => _store = new S3BlobStore(await S3Server.NewBucketAsync());

    [OneTimeTearDown]
    public void DisposeStore() => _store.Dispose();

    protected override IBlobStore Create() => _store;
}
