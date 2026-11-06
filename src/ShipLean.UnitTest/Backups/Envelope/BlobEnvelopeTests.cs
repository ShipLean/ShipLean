using System.Text;
using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.UnitTest.Backups.Envelope;

[TestFixture]
public class BlobEnvelopeTests
{
    [Test]
    public async Task Envelope_WhenWrittenAndRead_ShouldRoundTripKeyTypeAndBytes()
    {
        var bytes = new byte[300_000];
        new Random(781).NextBytes(bytes);
        using var envelope = new MemoryStream();
        await BlobEnvelope.WriteAsync(envelope, "attachments/9f1c2d3e", "image/png", new MemoryStream(bytes), CancellationToken.None);
        envelope.Position = 0;

        var (key, contentType, content) = await BlobEnvelope.ReadAsync(envelope);

        key.ShouldBe("attachments/9f1c2d3e");
        contentType.ShouldBe("image/png");
        using var rest = new MemoryStream();
        await content.CopyToAsync(rest);
        rest.ToArray().ShouldBe(bytes);
    }

    [Test]
    public async Task Envelope_WhenSourceNotSeekable_ShouldRoundTrip()
    {
        var bytes = Encoding.UTF8.GetBytes("not seekable, with a newline\nand non-ASCII: é中");
        using var source = new NonSeekableStream(bytes);
        using var envelope = new MemoryStream();
        await BlobEnvelope.WriteAsync(envelope, "notes/café.txt", "text/plain; charset=\"utf-8\"", source, CancellationToken.None);
        envelope.Position = 0;
        using var readFrom = new NonSeekableStream(envelope.ToArray());

        var (key, contentType, content) = await BlobEnvelope.ReadAsync(readFrom);

        key.ShouldBe("notes/café.txt");
        contentType.ShouldBe("text/plain; charset=\"utf-8\"");
        using var rest = new MemoryStream();
        await content.CopyToAsync(rest);
        rest.ToArray().ShouldBe(bytes);
    }

    [TestCase("")]
    [TestCase("SLBLOB2\n{\"key\":\"a\",\"contentType\":\"t\"}\nbody")]
    [TestCase("SLBLOB1\n{\"key\":\"a\",\"contentType\":\"t\"}")]
    [TestCase("SLBLOB1\nnot json\nbody")]
    [TestCase("SLBLOB1\n{\"key\":\"a\"}\nbody")]
    [TestCase("SLBLOB1\n{\"key\":\"a\",\"contentType\":null}\nbody")]
    [TestCase("SLBLOB1\nnull\nbody")]
    public async Task Envelope_WhenHeaderDamaged_ShouldThrow(string damaged)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(damaged));

        await Should.ThrowAsync<InvalidDataException>(async () => await BlobEnvelope.ReadAsync(input));
    }

    [Test]
    public async Task Envelope_WhenMetadataOverFourKilobytes_ShouldThrow()
    {
        using var output = new MemoryStream();
        await Should.ThrowAsync<ArgumentException>(async () => await BlobEnvelope.WriteAsync(
            output, "a", new string('t', BlobEnvelope.MaxMetadataBytes), new MemoryStream(), CancellationToken.None));
        output.Length.ShouldBe(0);

        var line = $"{{\"key\":\"a\",\"contentType\":\"{new string('t', BlobEnvelope.MaxMetadataBytes)}\"}}";
        using var oversized = new MemoryStream(Encoding.UTF8.GetBytes($"SLBLOB1\n{line}\nbody"));
        await Should.ThrowAsync<InvalidDataException>(async () => await BlobEnvelope.ReadAsync(oversized));
    }

    [Test]
    public async Task Envelope_WhenMetadataExactlyFourKilobytes_ShouldRoundTrip()
    {
        // {"key":"a","contentType":"..."} is 31 bytes around the content type.
        var contentType = new string('t', BlobEnvelope.MaxMetadataBytes - 31);
        using var envelope = new MemoryStream();
        await BlobEnvelope.WriteAsync(envelope, "a", contentType, new MemoryStream("x"u8.ToArray()), CancellationToken.None);
        envelope.Position = 0;

        var (_, readType, _) = await BlobEnvelope.ReadAsync(envelope);

        readType.ShouldBe(contentType);
    }

    [TestCase("../outside")]
    [TestCase("/rooted")]
    [TestCase("a/./b")]
    [TestCase("a.tmp")]
    [TestCase("")]
    public async Task Envelope_WhenKeyInvalid_ShouldThrow(string key)
    {
        using var output = new MemoryStream();
        await Should.ThrowAsync<ArgumentException>(async () => await BlobEnvelope.WriteAsync(
            output, key, "text/plain", new MemoryStream(), CancellationToken.None));
        output.Length.ShouldBe(0);

        var forged = $"SLBLOB1\n{{\"key\":\"{key}\",\"contentType\":\"text/plain\"}}\nbody";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(forged));
        await Should.ThrowAsync<InvalidDataException>(async () => await BlobEnvelope.ReadAsync(input));
    }

    // Reads and nothing else: no Length, no Position, no Seek.
    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
