using System.Text;
using ShipLean.Contracts.Backups;
using Shouldly;

namespace ShipLean.TestSupport.Backups;

// Behaviour every IBackupCipher must have, the pass-through fake as much as the SLBK1 cipher.
[TestFixture]
public abstract class CipherContract
{
    protected abstract IBackupCipher Create();

    // More than two of SLBK1's 1 MiB records, written in chunks that don't line up with them.
    [Test]
    public async Task Decrypt_WhenSamePassphrase_ShouldRoundTrip()
    {
        var cipher = Create();
        var plain = new byte[2_500_000];
        new Random(743).NextBytes(plain);
        using var encrypted = new MemoryStream();
        await using (var encryptor = cipher.OpenEncryptor(encrypted, "correct horse"))
        {
            for (var offset = 0; offset < plain.Length; offset += 100_003)
            {
                await encryptor.WriteAsync(plain.AsMemory(offset, Math.Min(100_003, plain.Length - offset)));
            }
        }

        encrypted.Position = 0;
        using var decrypted = new MemoryStream();
        await cipher.DecryptAsync(encrypted, decrypted, "correct horse", CancellationToken.None);

        decrypted.ToArray().ShouldBe(plain);
    }

    [Test]
    public async Task Decrypt_WhenWrongPassphrase_ShouldThrow()
    {
        var cipher = Create();
        using var encrypted = await EncryptAsync(cipher, "the database", "correct horse");
        using var decrypted = new MemoryStream();

        await Should.ThrowAsync<BackupDecryptException>(() => cipher.DecryptAsync(encrypted, decrypted, "wrong horse", CancellationToken.None));

        decrypted.Length.ShouldBe(0);
    }

    [Test]
    public async Task Encryptor_WhenDisposed_ShouldLeaveOutputOpen()
    {
        var cipher = Create();
        using var output = new MemoryStream();
        var encryptor = cipher.OpenEncryptor(output, "correct horse");
        await encryptor.WriteAsync(Encoding.UTF8.GetBytes("the database"));

        await encryptor.DisposeAsync();

        output.CanWrite.ShouldBeTrue();
        output.Length.ShouldBeGreaterThan("the database".Length);
        output.Position = 0;
        using var decrypted = new MemoryStream();
        await cipher.DecryptAsync(output, decrypted, "correct horse", CancellationToken.None);
        Encoding.UTF8.GetString(decrypted.ToArray()).ShouldBe("the database");
    }

    private static async Task<MemoryStream> EncryptAsync(IBackupCipher cipher, string text, string passphrase)
    {
        var encrypted = new MemoryStream();
        await using (var encryptor = cipher.OpenEncryptor(encrypted, passphrase))
        {
            await encryptor.WriteAsync(Encoding.UTF8.GetBytes(text));
        }

        encrypted.Position = 0;
        return encrypted;
    }
}
