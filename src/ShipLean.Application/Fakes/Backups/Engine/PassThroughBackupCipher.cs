using System.Security.Cryptography;
using System.Text;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Fakes.Backups.Engine;

// Stands in for the SLBK1 cipher without encrypting: SLBK-FAKE, then the first 8 bytes of SHA-256(passphrase), then
// the plain bytes. Decrypting checks that tag, so a wrong passphrase throws BackupDecryptException as the real cipher
// does.
public sealed class PassThroughBackupCipher : IBackupCipher
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("SLBK-FAKE");
    private const int TagLength = 8;

    public Stream OpenEncryptor(Stream output, string passphrase) => new Encryptor(output, Header(passphrase));

    public async Task DecryptAsync(Stream encrypted, Stream output, string passphrase, CancellationToken cancellationToken)
    {
        var header = new byte[Magic.Length + TagLength];
        try
        {
            await encrypted.ReadExactlyAsync(header, cancellationToken);
        }
        catch (EndOfStreamException e)
        {
            throw new BackupDecryptException("The file is too short to be a backup.", e);
        }

        if (!header.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            throw new BackupDecryptException("The file is not a backup.");
        }

        if (!CryptographicOperations.FixedTimeEquals(header, Header(passphrase)))
        {
            throw new BackupDecryptException("The passphrase is wrong, or the file is damaged.");
        }

        await encrypted.CopyToAsync(output, cancellationToken);
    }

    private static byte[] Header(string passphrase)
        => [.. Magic, .. SHA256.HashData(Encoding.UTF8.GetBytes(passphrase)).AsSpan(0, TagLength)];

    // Write-only. The header goes out with the first write, or on dispose when nothing was written; output stays open.
    private sealed class Encryptor(Stream output, byte[] header) : Stream
    {
        private bool _headerWritten;
        private bool _disposed;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => !_disposed;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            WriteHeaderOnce();
            output.Write(buffer, offset, count);
        }

        public override void Flush() => output.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                WriteHeaderOnce();
                output.Flush();
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        private void WriteHeaderOnce()
        {
            if (!_headerWritten)
            {
                output.Write(header);
                _headerWritten = true;
            }
        }
    }
}
