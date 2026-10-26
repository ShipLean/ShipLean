using System.Text;
using Amazon.S3;
using ShipLean.Application.Storage;
using ShipLean.TestSupport.Storage;

namespace ShipLean.WebApi.Tests.Storage;

// What S3BlobStore does on the bucket itself: where objects land, what the object's headers say, and the error for a
// bucket that isn't there.
[TestFixture]
public class S3BlobStoreTests
{
    [Test]
    public async Task PutAsync_WhenPrefixed_ShouldStoreTheObjectUnderPrefixAndKey()
    {
        var connection = await S3Server.NewBucketAsync("prod/");
        using var store = new S3BlobStore(connection);

        await store.PutAsync("covers/a.txt", new MemoryStream(Encoding.UTF8.GetBytes("hello")), "text/plain", CancellationToken.None);

        using var raw = S3Server.CreateClient(connection);
        (await raw.ListObjectsV2Async(new() { BucketName = connection.Bucket })).S3Objects.Select(o => o.Key).ShouldBe(["prod/covers/a.txt"]);
        using var response = await raw.GetObjectAsync(connection.Bucket, "prod/covers/a.txt");
        (await new StreamReader(response.ResponseStream).ReadToEndAsync()).ShouldBe("hello");
    }

    [Test]
    public async Task PutAsync_ShouldStoreTheContentTypeOnTheObject()
    {
        var connection = await S3Server.NewBucketAsync();
        using var store = new S3BlobStore(connection);

        await store.PutAsync("a.md", new MemoryStream([1, 2, 3]), "text/markdown", CancellationToken.None);

        using var raw = S3Server.CreateClient(connection);
        (await raw.GetObjectMetadataAsync(connection.Bucket, "a.md")).Headers.ContentType.ShouldBe("text/markdown");
    }

    [Test]
    public async Task GetAsync_ShouldGiveALengthAndServeRanges()
    {
        using var store = new S3BlobStore(await S3Server.NewBucketAsync());
        await store.PutAsync("a.bin", new MemoryStream([0, 1, 2, 3, 4, 5, 6, 7, 8, 9]), "application/octet-stream", CancellationToken.None);

        var blob = (await store.GetAsync("a.bin", CancellationToken.None)).ShouldNotBeNull();
        await using var content = blob.Content;

        content.CanSeek.ShouldBeTrue();
        content.Length.ShouldBe(10);
        content.Seek(4, SeekOrigin.Begin);
        var rest = new byte[6];
        await content.ReadExactlyAsync(rest);
        rest.ShouldBe([4, 5, 6, 7, 8, 9]);
        Should.Throw<NotSupportedException>(() => content.Seek(0, SeekOrigin.Begin));
    }

    [Test]
    public async Task Store_WhenBucketIsMissing_ShouldFailClearly()
    {
        var real = await S3Server.NewBucketAsync();
        using var store = new S3BlobStore(real with { Bucket = $"{real.Bucket[..30]}-missing" });

        var put = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.PutAsync("a.txt", new MemoryStream([1]), "text/plain", CancellationToken.None));
        var get = await Should.ThrowAsync<InvalidOperationException>(async () => await store.GetAsync("a.txt", CancellationToken.None));
        var exists = await Should.ThrowAsync<InvalidOperationException>(async () => await store.ExistsAsync("a.txt", CancellationToken.None));

        foreach (var error in new[] { put, get, exists })
        {
            error.Message.ShouldContain("does not exist");
            error.InnerException.ShouldBeOfType<AmazonS3Exception>();
        }
    }
}
