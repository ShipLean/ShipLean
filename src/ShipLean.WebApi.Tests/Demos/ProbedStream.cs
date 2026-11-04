namespace ShipLean.WebApi.Tests.Demos;

// A forward-only stream of zero bytes that notes how many bytes the stub had received when the client reached its end.
// A client that buffers the whole body before sending reads to the end with nothing received yet.
internal sealed class ProbedStream(long length, Func<long> received) : Stream
{
    private long _position;

    public long ReceivedAtEnd { get; private set; } = -1;

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var take = (int)Math.Min(buffer.Length, length - _position);
        buffer[..take].Clear();
        _position += take;
        if (_position == length && ReceivedAtEnd < 0)
        {
            ReceivedAtEnd = received();
        }

        return take;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => new(Read(buffer.Span));

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
