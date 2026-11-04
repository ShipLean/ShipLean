using System.Text;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Blobs;

namespace ShipLean.UnitTest.Storage;

// BlobRangeStream over a store that records every open and every close and hands bytes out three at a time.
[TestFixture]
public class BlobRangeStreamTests
{
    private const string Key = "attachments/a";
    private const string Text = "0123456789";

    [Test]
    public async Task Read_WhenSoughtFirst_ShouldReturnFromThereWithOneOpenAtThatOffset()
    {
        var (store, stream) = await ArrangeAsync();

        stream.Seek(6, SeekOrigin.Begin);
        var text = await ReadRestAsync(stream);

        text.ShouldBe("6789");
        store.Opens.ShouldBe([6L]);
    }

    [Test]
    public async Task Read_WhenNothingSoughtNorRead_ShouldOpenNothing()
    {
        var (store, stream) = await ArrangeAsync();

        stream.Length.ShouldBe(10);
        stream.Seek(4, SeekOrigin.Begin);

        store.Opens.ShouldBeEmpty();
    }

    [Test]
    public async Task Read_WhenTheStoreAnswersInChunks_ShouldReturnAllOfItInOrder()
    {
        var (store, stream) = await ArrangeAsync();

        var text = await ReadRestAsync(stream);

        text.ShouldBe(Text);
        stream.Position.ShouldBe(10);
        store.Opens.ShouldBe([0L]);
    }

    [Test]
    public async Task Seek_WhenTwice_ShouldCloseTheFirstInnerStreamAndReopenAtTheLastPosition()
    {
        var (store, stream) = await ArrangeAsync();
        stream.Seek(2, SeekOrigin.Begin);
        var one = new byte[1];
        await stream.ReadExactlyAsync(one);

        stream.Seek(7, SeekOrigin.Begin);
        var text = await ReadRestAsync(stream);

        one.ShouldBe("2"u8.ToArray());
        text.ShouldBe("789");
        store.Opens.ShouldBe([2L, 7L]);
        store.Closed.ShouldBe([true, false]);
    }

    [Test]
    public async Task Dispose_WhenReadingHasStarted_ShouldCloseTheInnerStream()
    {
        var (store, stream) = await ArrangeAsync();
        await stream.ReadExactlyAsync(new byte[2]);
        store.Closed.ShouldBe([false]);

        await stream.DisposeAsync();

        store.Closed.ShouldBe([true]);
    }

    [Test]
    public async Task Read_WhenPositionIsTheLength_ShouldReturnZeroWithoutOpening()
    {
        var (store, stream) = await ArrangeAsync();
        stream.Seek(0, SeekOrigin.End);

        (await stream.ReadAsync(new byte[4])).ShouldBe(0);

        store.Opens.ShouldBeEmpty();
    }

    private static async Task<(RecordingStore Store, BlobRangeStream Stream)> ArrangeAsync()
    {
        var store = new RecordingStore();
        await store.PutAsync(Key, new MemoryStream(Encoding.ASCII.GetBytes(Text)), "video/mp4", CancellationToken.None);
        return (store, new BlobRangeStream(store, Key, Text.Length));
    }

    private static async Task<string> ReadRestAsync(Stream stream) => await new StreamReader(stream, Encoding.ASCII).ReadToEndAsync();

    private sealed class RecordingStore : IBlobStore
    {
        private readonly InMemoryBlobStore _inner = new();
        private readonly List<ChunkedStream> _streams = [];

        private readonly List<long> _opens = [];

        public IReadOnlyList<long> Opens => _opens;

        public IReadOnlyList<bool> Closed => _streams.ConvertAll(s => s.IsClosed);

        public Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
            => _inner.PutAsync(key, content, contentType, cancellationToken);

        public Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken) => _inner.GetAsync(key, cancellationToken);

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) => _inner.ExistsAsync(key, cancellationToken);

        public async Task<Stream?> OpenAtAsync(string key, long offset, CancellationToken cancellationToken)
        {
            _opens.Add(offset);
            var rest = await _inner.OpenAtAsync(key, offset, cancellationToken);
            var chunked = new ChunkedStream(rest!);
            _streams.Add(chunked);
            return chunked;
        }
    }

    // Reads at most three bytes a call, as a network stream does, and notes when it was closed.
    private sealed class ChunkedStream(Stream inner) : Stream
    {
        public bool IsClosed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, 3));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => inner.ReadAsync(buffer[..Math.Min(buffer.Length, 3)], cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void Flush()
        {
            // Read-only: nothing to flush.
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsClosed = true;
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
