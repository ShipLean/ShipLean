using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Crypto;

// The SLBK1 layout (docs/backup-file-format.md): a 36-byte header, then records of flags (1), plaintext length
// (uint32 BE), ciphertext and a 16-byte AES-256-GCM tag. The nonce is the header's 8-byte prefix and the uint32 BE
// record index; the AAD is the header, the index and the flags.
internal static class Slbk1Format
{
    public const int HeaderLength = 36;
    public const byte Version = 0x01;
    public const byte KdfPbkdf2Sha256 = 0x01;
    public const uint WriteIterations = 600_000;
    public const uint MinIterations = 600_000;
    public const uint MaxIterations = 2_000_000;
    public const int SaltLength = 16;
    public const int NoncePrefixLength = 8;
    public const int KeyLength = 32;
    public const int TagLength = 16;
    public const int NonceLength = 12;
    public const int RecordHeaderLength = 5;
    public const int AadLength = HeaderLength + 4 + 1;
    public const int MaxRecordLength = 1_048_576;
    public const byte FinalFlag = 0x01;

    private const int VersionOffset = 4;
    private const int KdfOffset = 5;
    private const int IterationsOffset = 6;
    private const int SaltOffset = 10;
    private const int NoncePrefixOffset = 26;
    private const int ReservedOffset = 34;

    private static ReadOnlySpan<byte> Magic => "SLBK"u8;

    // A new header with a random salt and nonce prefix.
    public static byte[] NewHeader()
    {
        var header = new byte[HeaderLength];
        Magic.CopyTo(header);
        header[VersionOffset] = Version;
        header[KdfOffset] = KdfPbkdf2Sha256;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(IterationsOffset, 4), WriteIterations);
        RandomNumberGenerator.Fill(header.AsSpan(SaltOffset, SaltLength));
        RandomNumberGenerator.Fill(header.AsSpan(NoncePrefixOffset, NoncePrefixLength));
        return header;
    }

    // Refuses a header SLBK1 doesn't allow, before any key is derived from it.
    public static void Check(ReadOnlySpan<byte> header)
    {
        if (!header[..Magic.Length].SequenceEqual(Magic))
        {
            throw new BackupDecryptException("The file is not a ShipLean backup.");
        }

        if (header[VersionOffset] != Version)
        {
            throw new BackupDecryptException($"The backup format version {header[VersionOffset]} is not supported.");
        }

        if (header[KdfOffset] != KdfPbkdf2Sha256)
        {
            throw new BackupDecryptException($"The backup key derivation {header[KdfOffset]} is not supported.");
        }

        var iterations = Iterations(header);
        if (iterations is < MinIterations or > MaxIterations)
        {
            throw new BackupDecryptException($"The backup's key derivation iterations ({iterations}) are outside {MinIterations} to {MaxIterations}.");
        }

        if (header[ReservedOffset] != 0 || header[ReservedOffset + 1] != 0)
        {
            throw new BackupDecryptException("The backup header's reserved bytes are not zero.");
        }
    }

    // PBKDF2-HMAC-SHA256 over the UTF-8 passphrase, with the header's salt and iterations. Only for a header that
    // passed Check (or one NewHeader made).
    public static byte[] DeriveKey(string passphrase, ReadOnlySpan<byte> header)
    {
        var secret = Encoding.UTF8.GetBytes(passphrase);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(secret, header.Slice(SaltOffset, SaltLength), (int)Iterations(header), HashAlgorithmName.SHA256, KeyLength);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public static void Nonce(ReadOnlySpan<byte> header, uint index, Span<byte> nonce)
    {
        header.Slice(NoncePrefixOffset, NoncePrefixLength).CopyTo(nonce);
        BinaryPrimitives.WriteUInt32BigEndian(nonce[NoncePrefixLength..], index);
    }

    public static void Aad(ReadOnlySpan<byte> header, uint index, byte flags, Span<byte> aad)
    {
        header.CopyTo(aad);
        BinaryPrimitives.WriteUInt32BigEndian(aad.Slice(HeaderLength, 4), index);
        aad[HeaderLength + 4] = flags;
    }

    private static uint Iterations(ReadOnlySpan<byte> header) => BinaryPrimitives.ReadUInt32BigEndian(header.Slice(IterationsOffset, 4));
}
