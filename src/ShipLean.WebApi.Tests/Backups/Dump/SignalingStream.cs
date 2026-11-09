using System.Text;

namespace ShipLean.WebApi.Tests.Backups.Dump;

// A write-only stream that remembers what was written and completes Written on the first write.
internal sealed class SignalingStream : Stream
{
    private readonly MemoryStream _bytes = new();
    private readonly TaskCompletionSource _written = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Written => _written.Task;

    public string Text => Encoding.UTF8.GetString(_bytes.ToArray());

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _bytes.Length;

    public override long Position { get => _bytes.Position; set => throw new NotSupportedException(); }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _bytes.Write(buffer, offset, count);
        _written.TrySetResult();
    }
}
