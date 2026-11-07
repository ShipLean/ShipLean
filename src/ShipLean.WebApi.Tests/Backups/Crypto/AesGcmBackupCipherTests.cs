using System.Buffers.Binary;
using System.Text;
using ShipLean.Application.Features.Backups.Crypto;
using ShipLean.Contracts.Backups;

namespace ShipLean.WebApi.Tests.Backups.Crypto;

// SLBK1 as docs/backup-file-format.md pins it. CipherContract also passes against the pass-through fake, so the
// format, tamper and truncation cases are all here.
[TestFixture]
public class AesGcmBackupCipherTests
{
    private const string Passphrase = "correct horse battery staple";
    private const string OtherPassphrase = "wrong horse battery staple";
    private const int Record = Slbk1Spec.MaxRecordLength;

    private readonly AesGcmBackupCipher _cipher = new();

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(Record)]
    [TestCase(3 * Record + Record / 2)]
    public async Task Encrypt_WhenDecrypted_ShouldRoundTrip(int size)
    {
        var plain = Plain(size);

        var file = Encrypt(plain);

        (await DecryptAsync(file)).ShouldBe(plain);
    }

    [Test]
    public async Task Decrypt_WhenWrongPassphrase_ShouldThrow()
    {
        var file = Encrypt(Plain(1000));
        using var output = new MemoryStream();

        await Should.ThrowAsync<BackupDecryptException>(() => _cipher.DecryptAsync(new MemoryStream(file), output, OtherPassphrase, CancellationToken.None));

        output.Length.ShouldBe(0);
    }

    // A file of a full record and a half one. A record whose tag fails releases none of its plaintext; the records
    // before it have been released already, which is why callers publish only after DecryptAsync returns.
    [TestCase("ciphertext", 0)]
    [TestCase("ciphertext", 1)]
    [TestCase("tag", 0)]
    [TestCase("tag", 1)]
    [TestCase("finalFlag", 0)]
    [TestCase("finalFlagCleared", 1)]
    [TestCase("unknownFlag", 0)]
    [TestCase("shorterLength", 1)]
    public async Task Decrypt_WhenRecordTampered_ShouldThrow(string change, int recordIndex)
    {
        var file = Encrypt(Plain(Record + Record / 2));
        var record = Slbk1Spec.Records(file)[recordIndex];
        switch (change)
        {
            case "ciphertext":
                file[record.Offset + 5 + 1000] ^= 0x01;
                break;
            case "tag":
                file[record.End - 1] ^= 0x80;
                break;
            case "finalFlag":
                file[record.Offset] = 0x01;
                break;
            case "finalFlagCleared":
                file[record.Offset] = 0x00;
                break;
            case "unknownFlag":
                file[record.Offset] = 0x02;
                break;
            case "shorterLength":
                BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(record.Offset + 1, 4), (uint)record.Length - 1);
                break;
        }

        using var output = new MemoryStream();
        await Should.ThrowAsync<BackupDecryptException>(() => _cipher.DecryptAsync(new MemoryStream(file), output, Passphrase, CancellationToken.None));

        output.Length.ShouldBe(recordIndex * Record);
    }

    [TestCase("finalRecordDropped")]
    [TestCase("finalTagCut")]
    [TestCase("finalRecordHeaderCut")]
    [TestCase("allRecordsDropped")]
    [TestCase("headerCut")]
    [TestCase("empty")]
    public async Task Decrypt_WhenFinalRecordMissing_ShouldThrow(string cut)
    {
        var file = Encrypt(Plain(Record + 10));
        var final = Slbk1Spec.Records(file)[^1];
        var length = cut switch
        {
            "finalRecordDropped" => final.Offset,
            "finalTagCut" => final.End - 1,
            "finalRecordHeaderCut" => final.Offset + 3,
            "allRecordsDropped" => Slbk1Spec.HeaderLength,
            "headerCut" => Slbk1Spec.HeaderLength - 1,
            _ => 0,
        };

        var error = await Should.ThrowAsync<BackupDecryptException>(() => DecryptAsync(file[..length]));

        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [TestCase("oneByte")]
    [TestCase("finalRecordAgain")]
    [TestCase("emptyRecord")]
    public async Task Decrypt_WhenDataAfterFinal_ShouldThrow(string extra)
    {
        var file = Encrypt(Plain(100));
        var final = Slbk1Spec.Records(file)[^1];
        byte[] tail = extra switch
        {
            "oneByte" => [0x00],
            "finalRecordAgain" => file[final.Offset..final.End],
            _ => new byte[5 + Slbk1Spec.TagLength],
        };

        var error = await Should.ThrowAsync<BackupDecryptException>(() => DecryptAsync([.. file, .. tail]));

        error.Message.ShouldContain("after its final record");
    }

    [TestCase("swapped")]
    [TestCase("firstDropped")]
    [TestCase("firstRepeated")]
    public async Task Decrypt_WhenRecordsReordered_ShouldThrow(string order)
    {
        var file = Encrypt(Plain(2 * Record + 100));
        var records = Slbk1Spec.Records(file);
        var header = file[..Slbk1Spec.HeaderLength];
        byte[] Bytes(int index) => file[records[index].Offset..records[index].End];
        byte[] reordered = order switch
        {
            "swapped" => [.. header, .. Bytes(1), .. Bytes(0), .. Bytes(2)],
            "firstDropped" => [.. header, .. Bytes(1), .. Bytes(2)],
            _ => [.. header, .. Bytes(0), .. Bytes(0), .. Bytes(1), .. Bytes(2)],
        };

        await Should.ThrowAsync<BackupDecryptException>(() => DecryptAsync(reordered));
    }

    [TestCase(0, "magic")]
    [TestCase(4, "version")]
    [TestCase(5, "key derivation")]
    [TestCase(9, "iterations")]
    [TestCase(Slbk1Spec.SaltOffset, "salt")]
    [TestCase(Slbk1Spec.SaltOffset + 15, "salt")]
    [TestCase(Slbk1Spec.NoncePrefixOffset, "nonce prefix")]
    [TestCase(Slbk1Spec.NoncePrefixOffset + 7, "nonce prefix")]
    [TestCase(Slbk1Spec.ReservedOffset, "reserved")]
    [TestCase(Slbk1Spec.ReservedOffset + 1, "reserved")]
    public async Task Decrypt_WhenHeaderTampered_ShouldThrow(int offset, string field)
    {
        var file = Encrypt(Plain(100));
        file[offset] ^= 0x01;
        using var output = new MemoryStream();

        await Should.ThrowAsync<BackupDecryptException>(() => _cipher.DecryptAsync(new MemoryStream(file), output, Passphrase, CancellationToken.None), field);

        output.Length.ShouldBe(0);
    }

    // The header is bound into every record's AAD as well as into the key and nonce: a file whose records were sealed
    // without it is refused.
    [Test]
    public async Task Decrypt_WhenAadLacksHeader_ShouldThrow()
    {
        var file = Slbk1Spec.Build(Passphrase, [Plain(100)], headerInAad: false);

        await Should.ThrowAsync<BackupDecryptException>(() => DecryptAsync(file));
    }

    // int.MaxValue iterations would take many minutes to derive, so finishing within the minute shows the header was
    // refused first.
    [TestCase(0u)]
    [TestCase(599_999u)]
    [TestCase(2_000_001u)]
    [TestCase((uint)int.MaxValue)]
    [TestCase(uint.MaxValue)]
    public void Decrypt_WhenIterationsOutOfRange_ShouldThrowBeforeDeriving(uint iterations)
    {
        var file = Encrypt(Plain(100));
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(Slbk1Spec.IterationsOffset, 4), iterations);
        BackupDecryptException? error = null;

        Should.CompleteIn(
            () => Task.Run(async () => error = await Should.ThrowAsync<BackupDecryptException>(() => DecryptAsync(file))),
            TimeSpan.FromSeconds(60));

        error.ShouldNotBeNull().Message.ShouldContain("iterations");
    }

    [TestCase(600_000u)]
    [TestCase(2_000_000u)]
    public async Task Decrypt_WhenIterationsAtBounds_ShouldDecrypt(uint iterations)
    {
        var plain = Plain(100);
        var file = Slbk1Spec.Build(Passphrase, [plain], iterations);

        (await DecryptAsync(file)).ShouldBe(plain);
    }

    // A claimed length over 1 MiB is refused from the record header, before reading or allocating for it.
    [TestCase((uint)Record + 1)]
    [TestCase(uint.MaxValue)]
    public async Task Decrypt_WhenLengthOverCap_ShouldThrowWithoutAllocating(uint length)
    {
        var file = Encrypt([]);
        var recordHeader = new byte[5];
        BinaryPrimitives.WriteUInt32BigEndian(recordHeader.AsSpan(1), length);
        byte[] claimed = [.. file[..Slbk1Spec.HeaderLength], .. recordHeader];
        using var input = new MemoryStream(claimed);
        using var output = new MemoryStream();

        // A MemoryStream completes every read synchronously, so the whole call runs on this thread before it returns.
        var before = GC.GetAllocatedBytesForCurrentThread();
        var decrypt = _cipher.DecryptAsync(input, output, Passphrase, CancellationToken.None);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        decrypt.IsCompleted.ShouldBeTrue();
        (await Should.ThrowAsync<BackupDecryptException>(decrypt)).Message.ShouldContain("more than the");
        allocated.ShouldBeLessThan(4L * 1024 * 1024);
    }

    [Test]
    public void Encrypt_WhenSameInputTwice_ShouldDifferInSaltAndNonce()
    {
        var plain = Plain(100);

        var first = Encrypt(plain);
        var second = Encrypt(plain);

        first[Slbk1Spec.SaltOffset..(Slbk1Spec.SaltOffset + 16)].ShouldNotBe(second[Slbk1Spec.SaltOffset..(Slbk1Spec.SaltOffset + 16)]);
        first[Slbk1Spec.NoncePrefixOffset..(Slbk1Spec.NoncePrefixOffset + 8)].ShouldNotBe(second[Slbk1Spec.NoncePrefixOffset..(Slbk1Spec.NoncePrefixOffset + 8)]);
        first[Slbk1Spec.HeaderLength..].ShouldNotBe(second[Slbk1Spec.HeaderLength..]);
    }

    [Test]
    public async Task Encryptor_WhenDisposedTwice_ShouldWriteOneFinalRecord()
    {
        using var output = new MemoryStream();
        var encryptor = _cipher.OpenEncryptor(output, Passphrase);
        await encryptor.WriteAsync(Plain(10));

        await encryptor.DisposeAsync();
        var length = output.Length;
        await encryptor.DisposeAsync();

        output.Length.ShouldBe(length);
        Slbk1Spec.Records(output.ToArray()).Count(r => r.Flags == 0x01).ShouldBe(1);
        (await DecryptAsync(output.ToArray())).ShouldBe(Plain(10));
    }

    [Test]
    public void Encryptor_WhenDisposedTwiceSynchronously_ShouldWriteOneFinalRecord()
    {
        using var output = new MemoryStream();
        var encryptor = _cipher.OpenEncryptor(output, Passphrase);
        encryptor.Write(Plain(10));

        encryptor.Dispose();
        var length = output.Length;
        encryptor.Dispose();

        output.Length.ShouldBe(length);
        Slbk1Spec.Records(output.ToArray()).Count(r => r.Flags == 0x01).ShouldBe(1);
        Slbk1Spec.Open(output.ToArray(), Passphrase).ShouldBe(Plain(10));
    }

    [Test]
    public async Task Encryptor_WhenDisposed_ShouldLeaveOutputOpen()
    {
        using var output = new MemoryStream();
        var encryptor = _cipher.OpenEncryptor(output, Passphrase);
        await encryptor.WriteAsync(Plain(10));

        await encryptor.DisposeAsync();

        output.CanWrite.ShouldBeTrue();
        output.WriteByte(0x2A);
        output.Length.ShouldBeGreaterThan(Slbk1Spec.HeaderLength + 10);
    }

    [TestCase("wrongPassphrase")]
    [TestCase("tampered")]
    [TestCase("cutOff")]
    [TestCase("badHeader")]
    [TestCase("dataAfterFinal")]
    public async Task Decrypt_WhenFails_ShouldNotEchoPassphrase(string failure)
    {
        var file = Encrypt(Plain(100));
        var passphrase = Passphrase;
        switch (failure)
        {
            case "wrongPassphrase":
                passphrase = OtherPassphrase;
                break;
            case "tampered":
                file[^1] ^= 0x01;
                break;
            case "cutOff":
                file = file[..^1];
                break;
            case "badHeader":
                file[Slbk1Spec.ReservedOffset] = 0x01;
                break;
            default:
                file = [.. file, 0x00];
                break;
        }

        using var output = new MemoryStream();
        var error = await Should.ThrowAsync<BackupDecryptException>(() => _cipher.DecryptAsync(new MemoryStream(file), output, passphrase, CancellationToken.None));

        foreach (var secret in new[] { Passphrase, OtherPassphrase, "horse" })
        {
            error.Message.ShouldNotContain(secret);
            error.ToString().ShouldNotContain(secret);
        }
    }

    // The encryptor's bytes, read by the document's own description: header fields, 1 MiB records, an index-numbered
    // nonce and the header in the AAD.
    [Test]
    public void Encrypt_WhenReadBySpec_ShouldMatchFormat()
    {
        var plain = Plain(2 * Record + 1000);

        var file = Encrypt(plain);

        Encoding.ASCII.GetString(file, 0, 4).ShouldBe("SLBK");
        file[4].ShouldBe((byte)0x01);
        file[5].ShouldBe((byte)0x01);
        Slbk1Spec.Iterations(file).ShouldBe(600_000u);
        file[Slbk1Spec.ReservedOffset].ShouldBe((byte)0);
        file[Slbk1Spec.ReservedOffset + 1].ShouldBe((byte)0);
        var records = Slbk1Spec.Records(file);
        records.Select(r => (r.Flags, r.Length)).ShouldBe([(0x00, Record), (0x00, Record), (0x01, 1000)]);
        records[^1].End.ShouldBe(file.Length);
        Slbk1Spec.Open(file, Passphrase).ShouldBe(plain);
    }

    // Exactly 1 MiB fills one record, which goes out as it fills; the final record is then empty.
    [Test]
    public void Encrypt_WhenExactlyOneRecord_ShouldEndWithEmptyFinal()
    {
        var file = Encrypt(Plain(Record));

        Slbk1Spec.Records(file).Select(r => (r.Flags, r.Length)).ShouldBe([(0x00, Record), (0x01, 0)]);
    }

    [Test]
    public async Task Decrypt_WhenBuiltBySpec_ShouldRoundTrip()
    {
        byte[][] plains = [Plain(Record), Plain(5), []];
        var file = Slbk1Spec.Build(Passphrase, plains);

        (await DecryptAsync(file)).ShouldBe([.. plains[0], .. plains[1]]);
    }

    [Test]
    public void Encrypt_WhenEncrypted_ShouldNotContainPlaintext()
    {
        var plain = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("the database row ", 2000)));

        var file = Encrypt(plain);

        Encoding.ASCII.GetString(file).ShouldNotContain("the database row");
        file.Length.ShouldBe(Slbk1Spec.HeaderLength + 5 + plain.Length + Slbk1Spec.TagLength);
    }

    // About 2 MiB at any size: 16 MiB through the encryptor, or back, allocates a few MiB, not the size of the data.
    // The output streams are allocated up front, and a MemoryStream completes every call on this thread.
    [Test]
    public void Encrypt_WhenLarge_ShouldUseBoundedMemory()
    {
        var chunk = Plain(100_000);
        using var output = new MemoryStream(17 * Record);

        var before = GC.GetAllocatedBytesForCurrentThread();
        using (var encryptor = _cipher.OpenEncryptor(output, Passphrase))
        {
            for (var i = 0; i < 168; i++)
            {
                encryptor.Write(chunk);
            }
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        output.Length.ShouldBeGreaterThan(16_800_000);
        allocated.ShouldBeLessThan(4L * 1024 * 1024);
    }

    [Test]
    public async Task Decrypt_WhenLarge_ShouldUseBoundedMemory()
    {
        var file = Encrypt(Plain(16 * Record));
        using var input = new MemoryStream(file);
        using var output = new MemoryStream(16 * Record);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var decrypt = _cipher.DecryptAsync(input, output, Passphrase, CancellationToken.None);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        decrypt.IsCompleted.ShouldBeTrue();
        await decrypt;
        output.Length.ShouldBe(16L * Record);
        allocated.ShouldBeLessThan(4L * 1024 * 1024);
    }

    private static byte[] Plain(int size)
    {
        var plain = new byte[size];
        new Random(size).NextBytes(plain);
        return plain;
    }

    private byte[] Encrypt(byte[] plain)
    {
        using var output = new MemoryStream();
        using (var encryptor = _cipher.OpenEncryptor(output, Passphrase))
        {
            encryptor.Write(plain);
        }

        return output.ToArray();
    }

    private async Task<byte[]> DecryptAsync(byte[] file)
    {
        using var output = new MemoryStream();
        await _cipher.DecryptAsync(new MemoryStream(file), output, Passphrase, CancellationToken.None);
        return output.ToArray();
    }
}
