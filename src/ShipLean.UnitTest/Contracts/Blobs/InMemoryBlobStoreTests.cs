using ShipLean.Application.Fakes.Blobs;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport.Contracts.Blobs;

namespace ShipLean.UnitTest.Contracts.Blobs;

[TestFixture]
public class InMemoryBlobStoreTests : BlobStoreContract
{
    protected override IBlobStore Create() => new InMemoryBlobStore();
}
