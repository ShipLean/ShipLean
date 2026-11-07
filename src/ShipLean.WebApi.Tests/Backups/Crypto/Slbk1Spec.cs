using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace ShipLean.WebApi.Tests.Backups.Crypto;

// SLBK1 written from docs/backup-file-format.md alone, without the production code, so the tests pin the bytes
// rather than whatever the cipher happens to do.
public static class Slbk1Spec
{
    public const int HeaderLength = 36;
    public const int IterationsOffset = 6;
    public const int SaltOffset = 10;
    public const int NoncePrefixOffset = 26;
    public const int ReservedOffset = 34;
    public const int TagLength = 16;
    public const int MaxRecordLength = 1_048_576;

    // An SLBK1 file with one record per entry of plains; the last one is final. headerInAad false leaves the header
    // out of each record's AAD, which a correct reader must refuse. editHeader changes header bytes and finalFlags the
    // last record's flags before anything is sealed, so the file is still sealed correctly: only an explicit check
    // refuses it.
    public static byte[] Build(
        string passphrase,
        IReadOnlyList<byte[]> plains,
        uint iterations = 600_000,
        bool headerInAad = true,
        Action<byte[]>? editHeader = null,
        byte finalFlags = 0x01)
    {
        var header = new byte[HeaderLength];
        "SLBK"u8.CopyTo(header);
        header[4] = 0x01;
        header[5] = 0x01;
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(IterationsOffset), iterations);
        RandomNumberGenerator.Fill(header.AsSpan(SaltOffset, 16));
        RandomNumberGenerator.Fill(header.AsSpan(NoncePrefixOffset, 8));
        editHeader?.Invoke(header);

        using var aes = new AesGcm(Key(passphrase, header), TagLength);
        using var file = new MemoryStream();
        file.Write(header);
        for (var index = 0; index < plains.Count; index++)
        {
            var flags = index == plains.Count - 1 ? finalFlags : (byte)0x00;
            var plain = plains[index];
            var ciphertext = new byte[plain.Length];
            var tag = new byte[TagLength];
            var aad = Aad(header, (uint)index, flags);
            aes.Encrypt(Nonce(header, (uint)index), plain, ciphertext, tag, headerInAad ? aad : aad[HeaderLength..]);
            file.WriteByte(flags);
            var length = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, (uint)plain.Length);
            file.Write(length);
            file.Write(ciphertext);
            file.Write(tag);
        }

        return file.ToArray();
    }

    // The records of a well-formed file, in order.
    public static IReadOnlyList<Slbk1SpecRecord> Records(byte[] file)
    {
        var records = new List<Slbk1SpecRecord>();
        var offset = HeaderLength;
        while (offset < file.Length)
        {
            var record = new Slbk1SpecRecord(offset, file[offset], (int)BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(offset + 1, 4)));
            records.Add(record);
            offset = record.End;
        }

        return records;
    }

    // Decrypts a well-formed file the way the document describes, record by record.
    public static byte[] Open(byte[] file, string passphrase)
    {
        var header = file[..HeaderLength];
        using var aes = new AesGcm(Key(passphrase, header), TagLength);
        using var plain = new MemoryStream();
        var index = 0u;
        foreach (var record in Records(file))
        {
            var body = file.AsSpan(record.Offset + 5, record.Length);
            var tag = file.AsSpan(record.Offset + 5 + record.Length, TagLength);
            var output = new byte[record.Length];
            aes.Decrypt(Nonce(header, index), body, tag, output, Aad(header, index, record.Flags));
            plain.Write(output);
            index++;
        }

        return plain.ToArray();
    }

    public static uint Iterations(byte[] file) => BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(IterationsOffset, 4));

    private static byte[] Key(string passphrase, byte[] header)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(passphrase), header.AsSpan(SaltOffset, 16), (int)Iterations(header), HashAlgorithmName.SHA256, 32);

    private static byte[] Nonce(byte[] header, uint index)
    {
        var nonce = new byte[12];
        header.AsSpan(NoncePrefixOffset, 8).CopyTo(nonce);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8), index);
        return nonce;
    }

    private static byte[] Aad(byte[] header, uint index, byte flags)
    {
        var aad = new byte[HeaderLength + 5];
        header.CopyTo(aad, 0);
        BinaryPrimitives.WriteUInt32BigEndian(aad.AsSpan(HeaderLength), index);
        aad[HeaderLength + 4] = flags;
        return aad;
    }
}
