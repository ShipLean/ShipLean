using ShipLean.Application.Fakes.Blobs;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport.Contracts.Blobs;

namespace ShipLean.UnitTest.Contracts.Blobs;

[TestFixture]
public class InMemoryBlobStoreTests : BlobStoreContract
{
    protected override IBlobStore Create() => new InMemoryBlobStore();

    [Test]
    public async Task OpenAtAsync_WhenOffset_ShouldReturnTheRest()
    {
        var store = Create();
        await store.PutAsync("attachments/a", new MemoryStream([1, 2, 3, 4, 5]), "video/mp4", CancellationToken.None);

        await using var rest = await store.OpenAtAsync("attachments/a", 2, CancellationToken.None) ?? throw new InvalidOperationException("missing");
        using var copy = new MemoryStream();
        await rest.CopyToAsync(copy);

        copy.ToArray().ShouldBe([3, 4, 5]);
    }

    [Test]
    public async Task OpenAtAsync_WhenKeyMissing_ShouldBeNull()
    {
        var store = Create();

        (await store.OpenAtAsync("attachments/none", 0, CancellationToken.None)).ShouldBeNull();
    }
}
