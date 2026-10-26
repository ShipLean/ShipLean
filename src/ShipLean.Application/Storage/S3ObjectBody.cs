using Amazon.S3.Model;

namespace ShipLean.Application.Storage;

// The body of an S3 object as a stream with a Length (from the response's content length), so a download gets a
// Content-Length. It is seekable only forward: a seek reads and discards, which is all a range request needs, and a
// seek backwards throws. Disposing it disposes the GetObject response, which releases the HTTP connection.
public sealed class S3ObjectBody(GetObjectResponse response) : Stream
{
    private readonly Stream _source = response.ResponseStream;
    private long _position;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => response.ContentLength;

    public override long Position
    {
        get => _position;
        set => _position = SkipTo(value);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        _position += read;
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            _ => Length + offset,
        };
        return _position;
    }

    private long SkipTo(long target)
    {
        if (target < _position)
        {
            throw new NotSupportedException("An S3 object body can only be read forward.");
        }

        var position = _position;
        var skip = new byte[Math.Min(target - position, 81920)];
        while (position < target)
        {
            var read = _source.Read(skip, 0, (int)Math.Min(skip.Length, target - position));
            if (read == 0)
            {
                break;
            }

            position += read;
        }

        return position;
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
            response.Dispose();
        }
        base.Dispose(disposing);
    }
}
