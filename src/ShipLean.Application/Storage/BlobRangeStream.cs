using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Storage;

// A read-only, seekable view of a stored blob whose length the caller already knows (the attachment row's size_bytes), because a
// store's stream can't say. It holds no store stream until the first read: a seek closes the one it has, and the next read opens
// the store at the new position. A response that serves a range seeks once and reads once, so one request makes one ranged read
// and never the whole blob. Disposing closes the inner stream.
public sealed class BlobRangeStream(IBlobStore store, string key, long length) : Stream
{
    private Stream? _inner;
    private long _position;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => length;

    public override long Position
    {
        get => _position;
        set => MoveTo(value);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => buffer.IsEmpty || _position >= length ? ValueTask.FromResult(0) : FillAsync(buffer, cancellationToken);

    private async ValueTask<int> FillAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        _inner ??= await store.OpenAtAsync(key, _position, cancellationToken)
            ?? throw new FileNotFoundException($"Blob '{key}' is gone.");
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        _position += read;
        return read;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("Read this stream asynchronously.");

    public override long Seek(long offset, SeekOrigin origin)
    {
        MoveTo(origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => length + offset,
        });
        return _position;
    }

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
            _inner?.Dispose();
            _inner = null;
        }

        base.Dispose(disposing);
    }

    private void MoveTo(long target)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(target);
        _inner?.Dispose();
        _inner = null;
        _position = target;
    }
}
