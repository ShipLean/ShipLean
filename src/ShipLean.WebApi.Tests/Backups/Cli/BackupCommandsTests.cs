using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Backups.Cli;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Modules;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups;
using ShipLean.LocalEnvironment.Database;

namespace ShipLean.WebApi.Tests.Backups.Cli;

// SHIP-793: the decrypt-backup and restore-blobs verbs, run through BackupCommands.RunAsync with the cipher from DI.
[TestFixture]
public class BackupCommandsTests
{
    private const string Passphrase = "correct horse battery staple";
    private const string OtherPassphrase = "wrong horse battery staple";
    private const int MultiRecordSize = 1_500_000;

    private string _dir = null!;
    private string _blobRoot = null!;
    private IBackupCipher _cipher = null!;

    [SetUp]
    public void CreateFolders()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"shiplean-cli-test-{Guid.NewGuid():N}");
        _blobRoot = Path.Combine(_dir, "blobs");
        Directory.CreateDirectory(_dir);
        var services = new ServiceCollection();
        new BackupCipherServices().AddServices(services, new ConfigurationBuilder().Build());
        _cipher = services.BuildServiceProvider().GetRequiredService<IBackupCipher>();
    }

    [TearDown]
    public void DeleteFolders() => Directory.Delete(_dir, recursive: true);

    [Test]
    public async Task Decrypt_WhenFile_ShouldWriteDump()
    {
        var source = Encrypted("in/db.dump.slbk", Plain(1000), Passphrase);
        var target = Path.Combine(_dir, "out.dump");

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", target]);

        run.Exit.ShouldBe(0);
        (await File.ReadAllBytesAsync(target)).ShouldBe(Plain(1000));
        File.Exists(target + ".partial").ShouldBeFalse();
    }

    [Test]
    public async Task Decrypt_WhenFolder_ShouldDecryptAllAndStripSuffix()
    {
        Encrypted("in/database/a.dump.slbk", Plain(10), Passphrase);
        Encrypted("in/attachments/g1/deep/b.slbk", Plain(20), Passphrase);
        var target = Path.Combine(_dir, "out");

        var run = await RunAsync(["decrypt-backup", "--in", Path.Combine(_dir, "in"), "--out", target]);

        run.Exit.ShouldBe(0);
        (await File.ReadAllBytesAsync(Path.Combine(target, "database", "a.dump"))).ShouldBe(Plain(10));
        (await File.ReadAllBytesAsync(Path.Combine(target, "attachments", "g1", "deep", "b"))).ShouldBe(Plain(20));
    }

    [Test]
    public async Task Decrypt_WhenWrongPassphrase_ShouldExitOneWithoutEchoingIt()
    {
        var source = Encrypted("in/db.slbk", Plain(10), Passphrase);

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", Path.Combine(_dir, "out")], OtherPassphrase);

        run.Exit.ShouldBe(1);
        run.Error.ShouldContain(source);
        (run.Output + run.Error).ShouldNotContain(OtherPassphrase);
    }

    [Test]
    public async Task Decrypt_WhenFails_ShouldLeaveNoOutputFile()
    {
        // Cut off after the first record has decrypted, so plaintext reached the partial file before the failure.
        var source = Encrypted("in/db.slbk", Plain(MultiRecordSize), Passphrase);
        var bytes = await File.ReadAllBytesAsync(source);
        await File.WriteAllBytesAsync(source, bytes[..^100]);
        var target = Path.Combine(_dir, "out");

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", target]);

        run.Exit.ShouldBe(1);
        File.Exists(target).ShouldBeFalse();
        File.Exists(target + ".partial").ShouldBeFalse();
    }

    [Test]
    public async Task Decrypt_WhenNoArguments_ShouldExitTwo()
    {
        var run = await RunAsync(["decrypt-backup"]);

        run.Exit.ShouldBe(2);
    }

    [Test]
    public async Task Decrypt_WhenOutputExists_ShouldExitTwoAndLeaveIt()
    {
        var source = Encrypted("in/db.slbk", Plain(10), Passphrase);
        var target = Path.Combine(_dir, "out");
        await File.WriteAllTextAsync(target, "keep me");

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", target]);

        run.Exit.ShouldBe(2);
        (await File.ReadAllTextAsync(target)).ShouldBe("keep me");
    }

    [Test]
    public async Task Decrypt_WhenPartialPathIsSymlink_ShouldRefuse()
    {
        var source = Encrypted("in/db.slbk", Plain(10), Passphrase);
        var target = Path.Combine(_dir, "out");
        var victim = Path.Combine(_dir, "victim");
        await File.WriteAllTextAsync(victim, "untouched");
        File.CreateSymbolicLink(target + ".partial", victim);

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", target]);

        run.Exit.ShouldBe(2);
        (await File.ReadAllTextAsync(victim)).ShouldBe("untouched");
        File.Exists(target).ShouldBeFalse();
        new FileInfo(target + ".partial").LinkTarget.ShouldBe(victim);
    }

    [Test]
    public async Task RestoreBlobs_WhenObjectFailsToDecrypt_ShouldPutNothingForIt()
    {
        await EncryptedBlobAsync("in/good.slbk", "attachments/good", "text/plain", "good", Passphrase);
        await EncryptedBlobAsync("in/bad.slbk", "attachments/bad", "text/plain", "bad", OtherPassphrase);

        var run = await RunAsync(["restore-blobs", "--in", Path.Combine(_dir, "in")]);

        run.Exit.ShouldBe(1);
        var store = new LocalDiskBlobStore(_blobRoot);
        (await store.ExistsAsync("attachments/good", CancellationToken.None)).ShouldBeTrue();
        (await store.ExistsAsync("attachments/bad", CancellationToken.None)).ShouldBeFalse();
        run.Error.ShouldContain("bad.slbk");
    }

    [Test]
    public async Task RestoreBlobs_WhenDiskStore_ShouldPutKeysWithContentType()
    {
        await EncryptedBlobAsync("in/g1/a.slbk", "attachments/one", "image/png", "pixels", Passphrase);

        var run = await RunAsync(["restore-blobs", "--in", Path.Combine(_dir, "in")]);

        run.Exit.ShouldBe(0);
        await AssertBlobAsync(new LocalDiskBlobStore(_blobRoot), "attachments/one", "image/png", "pixels");
    }

    [Test]
    public async Task RestoreBlobs_WhenKeyExists_ShouldSkipUnlessOverwrite()
    {
        var store = new LocalDiskBlobStore(_blobRoot);
        await store.PutAsync("attachments/one", new MemoryStream("old"u8.ToArray()), "text/plain", CancellationToken.None);
        await EncryptedBlobAsync("in/a.slbk", "attachments/one", "text/html", "new", Passphrase);

        var skipped = await RunAsync(["restore-blobs", "--in", Path.Combine(_dir, "in")]);
        await AssertBlobAsync(store, "attachments/one", "text/plain", "old");
        var overwritten = await RunAsync(["restore-blobs", "--in", Path.Combine(_dir, "in"), "--overwrite"]);

        skipped.Exit.ShouldBe(0);
        overwritten.Exit.ShouldBe(0);
        await AssertBlobAsync(store, "attachments/one", "text/html", "new");
    }

    [Test]
    public async Task RestoreBlobs_WhenEnvelopeDamaged_ShouldExitOneNamingFile()
    {
        var source = Encrypted("in/damaged.slbk", Encoding.UTF8.GetBytes("not an envelope"), Passphrase);

        var run = await RunAsync(["restore-blobs", "--in", Path.Combine(_dir, "in")]);

        run.Exit.ShouldBe(1);
        run.Error.ShouldContain(source);
    }

    [Test]
    public async Task RestoreBlobs_WhenPostgresStore_ShouldResolveAndPut()
    {
        await using var database = await FreshDatabase.CreateAsync(includeSeed: false);
        PostgresBlobStore.ApplySchema(DatabaseServices.PinUtc(database.ConnectionString));
        await EncryptedBlobAsync("in/a.slbk", "attachments/pg", "image/gif", "gifbytes", Passphrase);

        var run = await RunAsync(
            ["restore-blobs", "--in", Path.Combine(_dir, "in")],
            Passphrase,
            (BlobStoreServices.DbConnectionKey, database.ConnectionString));

        run.Exit.ShouldBe(0, run.Error);
        await using var store = new PostgresBlobStore(DatabaseServices.PinUtc(database.ConnectionString));
        await AssertBlobAsync(store, "attachments/pg", "image/gif", "gifbytes");
    }

    [Test]
    public void Handles_WhenUnknownVerb_ShouldBeFalse()
    {
        BackupCommands.Handles("serve").ShouldBeFalse();
        BackupCommands.Handles("decrypt-backup").ShouldBeTrue();
        BackupCommands.Handles("restore-blobs").ShouldBeTrue();
    }

    private async Task<CliRun> RunAsync(string[] args, string? passphrase = Passphrase, (string Key, string Value)? extra = null)
    {
        var settings = new Dictionary<string, string?>
        {
            [BlobStoreServices.RootKey] = _blobRoot,
            [BackupTempFiles.TempKey] = _dir,
            [BackupCommands.PassphraseKey] = passphrase,
        };
        if (extra is { } pair)
        {
            settings[pair.Key] = pair.Value;
        }

        var output = new StringWriter();
        var error = new StringWriter();
        var exit = await BackupCommands.RunAsync(args, TextReader.Null, output, error, new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return new CliRun(exit, output.ToString(), error.ToString());
    }

    private static byte[] Plain(int size) => Enumerable.Range(0, size).Select(i => (byte)(i * 31 % 251)).ToArray();

    private string Encrypted(string relativePath, byte[] plain, string passphrase)
    {
        var path = Path.Combine(_dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var file = File.Create(path);
        using (var encryptor = _cipher.OpenEncryptor(file, passphrase))
        {
            encryptor.Write(plain);
        }

        return path;
    }

    private async Task EncryptedBlobAsync(string relativePath, string key, string contentType, string content, string passphrase)
    {
        using var envelope = new MemoryStream();
        await BlobEnvelope.WriteAsync(envelope, key, contentType, new MemoryStream(Encoding.UTF8.GetBytes(content)), CancellationToken.None);
        Encrypted(relativePath, envelope.ToArray(), passphrase);
    }

    private static async Task AssertBlobAsync(ShipLean.Contracts.Blobs.IBlobStore store, string key, string contentType, string content)
    {
        var blob = await store.GetAsync(key, CancellationToken.None);
        blob.ShouldNotBeNull();
        blob.ContentType.ShouldBe(contentType);
        using var reader = new StreamReader(blob.Content);
        (await reader.ReadToEndAsync()).ShouldBe(content);
    }

    private sealed record CliRun(int Exit, string Output, string Error);
}
