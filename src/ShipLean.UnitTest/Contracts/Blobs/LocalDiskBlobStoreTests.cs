using ShipLean.Application.Storage;
using ShipLean.Contracts.Blobs;
using ShipLean.TestSupport.Contracts.Blobs;
using Shouldly;

namespace ShipLean.UnitTest.Contracts.Blobs;

[TestFixture]
public class LocalDiskBlobStoreTests : BlobStoreContract
{
    private string _root = null!;

    [SetUp]
    public void CreateRoot() => _root = Path.Combine(Path.GetTempPath(), "shiplean-unit-blobs", Guid.NewGuid().ToString("N"));

    [TearDown]
    public void DeleteRoot()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    protected override IBlobStore Create() => new LocalDiskBlobStore(_root);

    [Test]
    public async Task PutAsync_WhenKeyTraverses_ShouldNotWriteOutsideTheRoot()
    {
        var store = Create();
        var outside = Path.Combine(Path.GetDirectoryName(_root)!, "escaped.txt");

        await Should.ThrowAsync<ArgumentException>(async () => await store.PutAsync(
            "../escaped.txt", new MemoryStream([1]), "text/plain", CancellationToken.None));

        File.Exists(outside).ShouldBeFalse();
    }

    [Test]
    public async Task GetAsync_ShouldExposeOnlyTheBodyAsASeekableStream()
    {
        var store = Create();
        await store.PutAsync("covers/a.bin", new MemoryStream([1, 2, 3, 4, 5]), "application/x-test", CancellationToken.None);

        var blob = (await store.GetAsync("covers/a.bin", CancellationToken.None)).ShouldNotBeNull();
        await using var content = blob.Content;
        content.Length.ShouldBe(5);
        content.Seek(2, SeekOrigin.Begin);
        var rest = new byte[3];
        await content.ReadExactlyAsync(rest);

        rest.ShouldBe(new byte[] { 3, 4, 5 });
        blob.ContentType.ShouldBe("application/x-test");
        Directory.GetFiles(Path.Combine(_root, "covers")).ShouldBe([Path.Combine(_root, "covers", "a.bin")]);
    }

    [Test]
    public async Task PutAsync_WhenSegmentIsASymlink_ShouldRefuse()
    {
        var store = Create();
        var outside = Path.Combine(Path.GetDirectoryName(_root)!, "linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(_root, "covers"), outside);

            await Should.ThrowAsync<ArgumentException>(async () => await store.PutAsync(
                "covers/a.png", new MemoryStream([1]), "image/png", CancellationToken.None));

            Directory.GetFileSystemEntries(outside).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }
}
