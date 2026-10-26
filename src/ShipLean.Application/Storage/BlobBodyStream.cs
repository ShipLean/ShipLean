namespace ShipLean.Application.Storage;

// The bytes of a blob file after its header: a read-only, seekable view whose position 0 is the first body byte, so a
// response can use Length for Content-Length and serve ranges.
public sealed class BlobBodyStream(FileStream file, long offset) : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => file.Length - offset;

    public override long Position
    {
        get => file.Position - offset;
        set => file.Position = offset + value;
    }

    public override int Read(byte[] buffer, int offset, int count) => file.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            _ => Length + offset,
        };
        Position = target;
        return target;
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
            file.Dispose();
        }
        base.Dispose(disposing);
    }
}
