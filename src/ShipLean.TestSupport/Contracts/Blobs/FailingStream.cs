using System.Text;

namespace ShipLean.TestSupport.Contracts.Blobs;

// Yields its text, then throws IOException on the next read: an upload that dies part-way.
public sealed class FailingStream(string text) : Stream
{
    private readonly byte[] _bytes = Encoding.UTF8.GetBytes(text);
    private int _position;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_position >= _bytes.Length)
        {
            throw new IOException("The upload was cut off.");
        }

        var n = Math.Min(buffer.Length, _bytes.Length - _position);
        _bytes.AsSpan(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Read(buffer.Span));

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
