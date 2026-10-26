using System.Text;
using ShipLean.Contracts.Blobs;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Blobs;

// Behaviour every IBlobStore must have, whatever it stores data in.
[TestFixture]
public abstract class BlobStoreContract
{
    protected abstract IBlobStore Create();

    [Test]
    public async Task PutAsync_WhenKeyIsNew_ShouldStoreBytesAndContentType()
    {
        var store = Create();

        await store.PutAsync("covers/a.png", Bytes("png-bytes"), "image/png", CancellationToken.None);

        (await store.ExistsAsync("covers/a.png", CancellationToken.None)).ShouldBeTrue();
        var blob = (await store.GetAsync("covers/a.png", CancellationToken.None)).ShouldNotBeNull();
        blob.ContentType.ShouldBe("image/png");
        await using var content = blob.Content;
        (await new StreamReader(content).ReadToEndAsync()).ShouldBe("png-bytes");
    }

    [Test]
    public async Task PutAsync_WhenKeyExists_ShouldReplaceIt()
    {
        var store = Create();
        await store.PutAsync("a.txt", Bytes("one"), "text/plain", CancellationToken.None);

        await store.PutAsync("a.txt", Bytes("two"), "text/markdown", CancellationToken.None);

        var blob = (await store.GetAsync("a.txt", CancellationToken.None)).ShouldNotBeNull();
        blob.ContentType.ShouldBe("text/markdown");
        await using var content = blob.Content;
        (await new StreamReader(content).ReadToEndAsync()).ShouldBe("two");
    }

    [Test]
    public async Task GetAsync_WhenKeyIsMissing_ShouldReturnNull()
    {
        (await Create().GetAsync("nothing/here", CancellationToken.None)).ShouldBeNull();
    }

    [Test]
    public async Task PutAsync_ShouldReturnTheByteCount()
    {
        var count = await Create().PutAsync("a.txt", Bytes("twelve bytes"), "text/plain", CancellationToken.None);

        count.ShouldBe(12);
    }

    [Test]
    public async Task PutAsync_WhenStreamFails_ShouldKeepThePreviousBlob()
    {
        var store = Create();
        await store.PutAsync("a.txt", Bytes("previous"), "text/plain", CancellationToken.None);

        await Should.ThrowAsync<IOException>(async () => await store.PutAsync(
            "a.txt", new FailingStream("partial"), "text/markdown", CancellationToken.None));

        var blob = (await store.GetAsync("a.txt", CancellationToken.None)).ShouldNotBeNull();
        blob.ContentType.ShouldBe("text/plain");
        await using var content = blob.Content;
        (await new StreamReader(content).ReadToEndAsync()).ShouldBe("previous");
    }

    [Test]
    public async Task PutAsync_WhenStreamFailsOnANewKey_ShouldStoreNothing()
    {
        var store = Create();

        await Should.ThrowAsync<IOException>(async () => await store.PutAsync(
            "fresh.txt", new FailingStream("partial"), "text/plain", CancellationToken.None));

        (await store.ExistsAsync("fresh.txt", CancellationToken.None)).ShouldBeFalse();
    }

    [Test]
    public async Task PutAsync_WhenConcurrentOnSameKey_ShouldLeaveOneWholeBlob()
    {
        var store = Create();
        // Small payloads and many rounds, so the puts really overlap.
        var payloads = Enumerable.Range(0, 8).Select(i => new string((char)('a' + i), 1_000)).ToList();

        for (var round = 0; round < 100; round++)
        {
            await Task.WhenAll(payloads.Select((payload, i) => Task.Run(
                () => store.PutAsync("race.txt", Bytes(payload), $"text/x-{i}", CancellationToken.None))));

            var blob = (await store.GetAsync("race.txt", CancellationToken.None)).ShouldNotBeNull();
            await using var content = blob.Content;
            var text = await new StreamReader(content).ReadToEndAsync();
            var index = payloads.IndexOf(text);
            index.ShouldBeGreaterThanOrEqualTo(0);
            blob.ContentType.ShouldBe($"text/x-{index}", $"round {round}");
        }
    }

    [Test]
    public async Task GetAsync_WhenPutInProgress_ShouldReturnTheOldOrNewBlobWhole()
    {
        var store = Create();
        var oldText = new string('o', 1_000);
        var newText = new string('n', 1_000);
        await store.PutAsync("busy.txt", Bytes(oldText), "text/old", CancellationToken.None);

        var writer = Task.Run(async () =>
        {
            for (var i = 0; i < 300; i++)
            {
                var isNew = i % 2 == 0;
                await store.PutAsync("busy.txt", Bytes(isNew ? newText : oldText), isNew ? "text/new" : "text/old", CancellationToken.None);
            }
        });
        var reads = new List<(string Text, string Type)>();
        while (!writer.IsCompleted)
        {
            var blob = (await store.GetAsync("busy.txt", CancellationToken.None)).ShouldNotBeNull();
            await using var content = blob.Content;
            reads.Add((await new StreamReader(content).ReadToEndAsync(), blob.ContentType));
        }

        await writer;
        reads.ShouldAllBe(read => (read.Text == oldText && read.Type == "text/old") || (read.Text == newText && read.Type == "text/new"));
    }

    [TestCase("../outside.txt")]
    [TestCase("covers/../../outside.txt")]
    [TestCase("/etc/passwd")]
    [TestCase("")]
    [TestCase("covers/a.png.tmp")]
    public async Task PutAsync_WhenKeyTraverses_ShouldReject(string key)
    {
        var store = Create();

        await Should.ThrowAsync<ArgumentException>(
            async () => await store.PutAsync(key, Bytes("x"), "text/plain", CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(async () => await store.GetAsync(key, CancellationToken.None));
        await Should.ThrowAsync<ArgumentException>(async () => await store.ExistsAsync(key, CancellationToken.None));
    }

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));
}
