using System.Buffers.Binary;
using System.Security.Cryptography;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Crypto;

// SLBK1 (docs/backup-file-format.md): AES-256-GCM records under a key derived from the passphrase with
// PBKDF2-HMAC-SHA256. Streams in about 2 MiB at any size. Decrypting releases a record only after its tag verifies,
// and refuses a header outside the format before deriving a key from it.
public sealed class AesGcmBackupCipher : IBackupCipher
{
    private const string WrongOrDamaged = "The passphrase is wrong, or the backup file is damaged.";
    private const string CutOff = "The backup file is cut off: its final record is missing.";

    public Stream OpenEncryptor(Stream output, string passphrase)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);
        return new Slbk1Encryptor(output, passphrase);
    }

    public async Task DecryptAsync(Stream encrypted, Stream output, string passphrase, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrEmpty(passphrase);

        var header = new byte[Slbk1Format.HeaderLength];
        if (!await ReadFullyAsync(encrypted, header, cancellationToken))
        {
            throw new BackupDecryptException("The file is too short to be a ShipLean backup.");
        }

        Slbk1Format.Check(header);
        var key = Slbk1Format.DeriveKey(passphrase, header);
        using var aes = new AesGcm(key, Slbk1Format.TagLength);
        CryptographicOperations.ZeroMemory(key);

        var recordHeader = new byte[Slbk1Format.RecordHeaderLength];
        var sealedRecord = new byte[Slbk1Format.MaxRecordLength + Slbk1Format.TagLength];
        var plain = new byte[Slbk1Format.MaxRecordLength];
        var nonce = new byte[Slbk1Format.NonceLength];
        var aad = new byte[Slbk1Format.AadLength];
        try
        {
            var final = false;
            long index = 0;
            while (!final)
            {
                if (!await ReadFullyAsync(encrypted, recordHeader, cancellationToken))
                {
                    throw new BackupDecryptException(CutOff);
                }

                var flags = recordHeader[0];
                if ((flags & ~Slbk1Format.FinalFlag) != 0)
                {
                    throw new BackupDecryptException($"Record {index} of the backup file has unknown flags.");
                }

                var length = BinaryPrimitives.ReadUInt32BigEndian(recordHeader.AsSpan(1));
                if (length > Slbk1Format.MaxRecordLength)
                {
                    throw new BackupDecryptException($"Record {index} of the backup file claims {length} bytes, more than the {Slbk1Format.MaxRecordLength} allowed.");
                }

                if (index > uint.MaxValue)
                {
                    throw new BackupDecryptException("The backup file has more records than SLBK1 can number.");
                }

                var size = (int)length;
                if (!await ReadFullyAsync(encrypted, sealedRecord.AsMemory(0, size + Slbk1Format.TagLength), cancellationToken))
                {
                    throw new BackupDecryptException(CutOff);
                }

                Slbk1Format.Nonce(header, (uint)index, nonce);
                Slbk1Format.Aad(header, (uint)index, flags, aad);
                try
                {
                    aes.Decrypt(nonce, sealedRecord.AsSpan(0, size), sealedRecord.AsSpan(size, Slbk1Format.TagLength), plain.AsSpan(0, size), aad);
                }
                catch (AuthenticationTagMismatchException e)
                {
                    throw new BackupDecryptException(WrongOrDamaged, e);
                }

                await output.WriteAsync(plain.AsMemory(0, size), cancellationToken);
                final = flags == Slbk1Format.FinalFlag;
                index++;
            }

            if (await encrypted.ReadAsync(recordHeader.AsMemory(0, 1), cancellationToken) > 0)
            {
                throw new BackupDecryptException("The backup file has data after its final record.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    // False when the stream ends before the buffer is full. Network streams return fewer bytes than asked for.
    private static async Task<bool> ReadFullyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        return read == buffer.Length;
    }
}
