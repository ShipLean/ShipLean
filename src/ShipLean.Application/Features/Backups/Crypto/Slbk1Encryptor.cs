using System.Buffers.Binary;
using System.Security.Cryptography;

namespace ShipLean.Application.Features.Backups.Crypto;

// Write-only. The header goes out when the stream opens. Plaintext is buffered until it fills a record (1 MiB), which
// then goes out at once as a non-final record; disposing writes what is left as the one final record (empty when
// nothing is left) and leaves output open. Holds about 2 MiB whatever the size written.
internal sealed class Slbk1Encryptor : Stream
{
    private readonly Stream _output;
    private readonly byte[] _header;
    private readonly AesGcm _aes;
    private readonly byte[] _plain = new byte[Slbk1Format.MaxRecordLength];
    private readonly byte[] _record = new byte[Slbk1Format.RecordHeaderLength + Slbk1Format.MaxRecordLength + Slbk1Format.TagLength];
    private int _buffered;
    private long _index;
    private bool _disposed;

    public Slbk1Encryptor(Stream output, string passphrase)
    {
        _output = output;
        _header = Slbk1Format.NewHeader();
        var key = Slbk1Format.DeriveKey(passphrase, _header);
        try
        {
            _aes = new AesGcm(key, Slbk1Format.TagLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        _output.Write(_header);
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => !_disposed;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (!buffer.IsEmpty)
        {
            var take = Math.Min(buffer.Length, _plain.Length - _buffered);
            buffer[..take].CopyTo(_plain.AsSpan(_buffered));
            _buffered += take;
            buffer = buffer[take..];
            if (_buffered == _plain.Length)
            {
                WriteRecord(0);
            }
        }
    }

    // Records are only written whole, so this flushes what has already gone to output, not the buffer.
    public override void Flush() => _output.Flush();

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            // Marked first, so a second Dispose never writes another final record, even when this one throws.
            _disposed = true;
            try
            {
                WriteRecord(Slbk1Format.FinalFlag);
                _output.Flush();
            }
            finally
            {
                _aes.Dispose();
                CryptographicOperations.ZeroMemory(_plain);
            }
        }

        base.Dispose(disposing);
    }

    private void WriteRecord(byte flags)
    {
        if (_index > uint.MaxValue)
        {
            throw new InvalidOperationException("The backup is too large for one SLBK1 file: the record index would overflow.");
        }

        var index = (uint)_index;
        Span<byte> nonce = stackalloc byte[Slbk1Format.NonceLength];
        Span<byte> aad = stackalloc byte[Slbk1Format.AadLength];
        Slbk1Format.Nonce(_header, index, nonce);
        Slbk1Format.Aad(_header, index, flags, aad);

        var length = _buffered;
        var record = _record.AsSpan(0, Slbk1Format.RecordHeaderLength + length + Slbk1Format.TagLength);
        record[0] = flags;
        BinaryPrimitives.WriteUInt32BigEndian(record.Slice(1, 4), (uint)length);
        _aes.Encrypt(
            nonce,
            _plain.AsSpan(0, length),
            record.Slice(Slbk1Format.RecordHeaderLength, length),
            record.Slice(Slbk1Format.RecordHeaderLength + length, Slbk1Format.TagLength),
            aad);
        _output.Write(record);
        _buffered = 0;
        _index++;
    }
}
