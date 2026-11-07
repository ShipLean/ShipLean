using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Backups.Cli;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Modules;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Backups;
using ShipLean.LocalEnvironment.Database;

namespace ShipLean.WebApi.Tests.Backups.Cli;

// The decrypt-backup and restore-blobs verbs (C-CLI), run in process through BackupCommands with their configuration
// given directly, against files encrypted by the registered IBackupCipher and a real disk or Postgres blob store.
[TestFixture]
public class BackupCommandsTests
{
    private const string Passphrase = "correct horse battery staple 7f3a";
    private const string WrongPassphrase = "not the passphrase 91c2";
    private const int Record = 1024 * 1024;

    private IHost _cipherHost = null!;
    private IBackupCipher _cipher = null!;
    private string _root = null!;

    private string BlobRoot => Path.Combine(_root, "blobs");
    private string TempFolder => Path.Combine(_root, "tmp");

    [OneTimeSetUp]
    public void ResolveCipher()
    {
        var builder = Host.CreateEmptyApplicationBuilder(null);
        new BackupCipherServices().AddServices(builder.Services, builder.Configuration);
        _cipherHost = builder.Build();
        _cipher = _cipherHost.Services.GetRequiredService<IBackupCipher>();
    }

    [OneTimeTearDown]
    public void DisposeCipher() => _cipherHost.Dispose();

    [SetUp]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), $"shiplean-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(TempFolder);
    }

    [TearDown]
    public void DeleteRoot() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Handles_WhenUnknownVerb_ShouldBeFalse()
    {
        BackupCommands.Handles("serve").ShouldBeFalse();
        BackupCommands.Handles("decrypt-backup").ShouldBeTrue();
        BackupCommands.Handles("restore-blobs").ShouldBeTrue();
    }

    [Test]
    public async Task Decrypt_WhenNoArguments_ShouldExitTwo()
    {
        var run = await RunAsync(["decrypt-backup"], stdin: Passphrase + "\n");

        run.Code.ShouldBe(2);
        run.Error.ShouldContain("Usage");
    }

    [Test]
    public async Task Decrypt_WhenFile_ShouldWriteDump()
    {
        var plain = Plain(2 * Record + 1000);
        var source = Path.Combine(_root, "in", "shiplean-20261107T020000Z-1a2b3c4d-g1.dump.slbk");
        await WriteEncryptedAsync(source, plain, Passphrase);
        var target = Path.Combine(_root, "out", "restore.dump");

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", target], stdin: Passphrase + "\n");

        run.Code.ShouldBe(0, run.Error);
        (await File.ReadAllBytesAsync(target)).ShouldBe(plain);
        File.Exists(target + ".partial").ShouldBeFalse();
        if (!OperatingSystem.IsWindows())
        {
            File.GetUnixFileMode(target).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Test]
    public async Task Decrypt_WhenFolder_ShouldDecryptAllAndStripSuffix()
    {
        var input = Path.Combine(_root, "in");
        var dump = Plain(5000);
        var attachment = Plain(300);
        await WriteEncryptedAsync(Path.Combine(input, "database", "shiplean-a.dump.slbk"), dump, Passphrase);
        await WriteEncryptedAsync(Path.Combine(input, "attachments", "g2", "attachments", "9f1e.slbk"), attachment, Passphrase);
        await File.WriteAllTextAsync(Path.Combine(input, "notes.txt"), "not a backup");
        var output = Path.Combine(_root, "out");

        var run = await RunAsync(["decrypt-backup", "--in", input, "--out", output], passphrase: Passphrase);

        run.Code.ShouldBe(0, run.Error);
        (await File.ReadAllBytesAsync(Path.Combine(output, "database", "shiplean-a.dump"))).ShouldBe(dump);
        (await File.ReadAllBytesAsync(Path.Combine(output, "attachments", "g2", "attachments", "9f1e"))).ShouldBe(attachment);
        Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).Count().ShouldBe(2);
    }

    [Test]
    public async Task Decrypt_WhenWrongPassphrase_ShouldExitOneWithoutEchoingIt()
    {
        var source = Path.Combine(_root, "in", "db.dump.slbk");
        await WriteEncryptedAsync(source, Plain(1000), Passphrase);
        var target = Path.Combine(_root, "db.dump");

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", target], stdin: WrongPassphrase + "\n");

        run.Code.ShouldBe(1);
        run.Error.ShouldContain(source);
        (run.Output + run.Error).ShouldNotContain(WrongPassphrase);
        (run.Output + run.Error).ShouldNotContain(Passphrase);
        File.Exists(target).ShouldBeFalse();
        File.Exists(target + ".partial").ShouldBeFalse();
    }

    [Test]
    public async Task Decrypt_WhenFails_ShouldLeaveNoOutputFile()
    {
        // Two whole records are released before the cut-off final record fails, so the .partial had plaintext in it.
        var source = Path.Combine(_root, "in", "db.dump.slbk");
        await WriteEncryptedAsync(source, Plain(2 * Record + 1000), Passphrase);
        await TruncateAsync(source, 10);
        var target = Path.Combine(_root, "db.dump");

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", target], passphrase: Passphrase);

        run.Code.ShouldBe(1);
        run.Error.ShouldContain(source);
        File.Exists(target).ShouldBeFalse();
        File.Exists(target + ".partial").ShouldBeFalse();
    }

    [Test]
    public async Task Decrypt_WhenOutputExists_ShouldExitTwoAndLeaveIt()
    {
        var source = Path.Combine(_root, "in", "db.dump.slbk");
        await WriteEncryptedAsync(source, Plain(1000), Passphrase);
        var target = Path.Combine(_root, "db.dump");
        await File.WriteAllTextAsync(target, "keep me");

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", target], passphrase: Passphrase);

        run.Code.ShouldBe(2);
        run.Error.ShouldContain(target);
        (await File.ReadAllTextAsync(target)).ShouldBe("keep me");
        File.Exists(target + ".partial").ShouldBeFalse();
    }

    [Test]
    public async Task Decrypt_WhenPartialPathIsSymlink_ShouldRefuse()
    {
        var source = Path.Combine(_root, "in", "db.dump.slbk");
        await WriteEncryptedAsync(source, Plain(1000), Passphrase);
        var target = Path.Combine(_root, "db.dump");
        var elsewhere = Path.Combine(_root, "elsewhere.dump");
        File.CreateSymbolicLink(target + ".partial", elsewhere);

        var run = await RunAsync(["decrypt-backup", "--in", source, "--out", target], passphrase: Passphrase);

        run.Code.ShouldBe(2);
        run.Error.ShouldContain(target + ".partial");
        File.Exists(elsewhere).ShouldBeFalse();
        File.Exists(target).ShouldBeFalse();
        new FileInfo(target + ".partial").LinkTarget.ShouldBe(elsewhere);
    }

    [Test]
    public async Task RestoreBlobs_WhenDiskStore_ShouldPutKeysWithContentType()
    {
        var input = Path.Combine(_root, "attachments");
        var photo = Plain(4000);
        var notes = Plain(Record + 10);
        await WriteBlobObjectAsync(input, "attachments/0d1c2b3a/photo", "image/png", photo, Passphrase);
        await WriteBlobObjectAsync(input, "attachments/7e6f5a4b/notes", "text/markdown", notes, Passphrase);

        var run = await RunAsync(["restore-blobs", "--in", input], passphrase: Passphrase);

        run.Code.ShouldBe(0, run.Error);
        var store = new LocalDiskBlobStore(BlobRoot);
        await ShouldHoldAsync(store, "attachments/0d1c2b3a/photo", "image/png", photo);
        await ShouldHoldAsync(store, "attachments/7e6f5a4b/notes", "text/markdown", notes);
        Directory.EnumerateFileSystemEntries(TempFolder).ShouldBeEmpty();
    }

    [Test]
    public async Task RestoreBlobs_WhenKeyExists_ShouldSkipUnlessOverwrite()
    {
        const string key = "attachments/0d1c2b3a/photo";
        var store = new LocalDiskBlobStore(BlobRoot);
        var old = Plain(100);
        var restored = Plain(200);
        await store.PutAsync(key, new MemoryStream(old), "image/jpeg", CancellationToken.None);
        var input = Path.Combine(_root, "attachments");
        await WriteBlobObjectAsync(input, key, "image/png", restored, Passphrase);

        var kept = await RunAsync(["restore-blobs", "--in", input], passphrase: Passphrase);

        kept.Code.ShouldBe(0, kept.Error);
        kept.Output.ShouldContain($"skipped {key}");
        await ShouldHoldAsync(store, key, "image/jpeg", old);

        var replaced = await RunAsync(["restore-blobs", "--in", input, "--overwrite"], passphrase: Passphrase);

        replaced.Code.ShouldBe(0, replaced.Error);
        await ShouldHoldAsync(store, key, "image/png", restored);
    }

    [Test]
    public async Task RestoreBlobs_WhenObjectFailsToDecrypt_ShouldPutNothingForIt()
    {
        // The bad object's first record (envelope and 1 MiB of content) decrypts before its cut-off end fails, so a
        // restore that streamed into the store would have put that prefix.
        var input = Path.Combine(_root, "attachments");
        var good = Plain(500);
        await WriteBlobObjectAsync(input, "attachments/a-good", "image/png", good, Passphrase);
        var bad = await WriteBlobObjectAsync(input, "attachments/b-bad", "image/png", Plain(Record + Record / 2), Passphrase);
        await TruncateAsync(bad, 10);

        var run = await RunAsync(["restore-blobs", "--in", input], passphrase: Passphrase);

        run.Code.ShouldBe(1);
        run.Error.ShouldContain(bad);
        var store = new LocalDiskBlobStore(BlobRoot);
        (await store.ExistsAsync("attachments/b-bad", CancellationToken.None)).ShouldBeFalse();
        await ShouldHoldAsync(store, "attachments/a-good", "image/png", good);
        Directory.EnumerateFileSystemEntries(TempFolder).ShouldBeEmpty();
    }

    [Test]
    public async Task RestoreBlobs_WhenEnvelopeDamaged_ShouldExitOneNamingFile()
    {
        var source = Path.Combine(_root, "attachments", "g1", "attachments", "x.slbk");
        await WriteEncryptedAsync(source, "not an envelope at all"u8.ToArray(), Passphrase);

        var run = await RunAsync(["restore-blobs", "--in", Path.Combine(_root, "attachments")], stdin: Passphrase + "\n");

        run.Code.ShouldBe(1);
        run.Error.ShouldContain(source);
        run.Error.ShouldContain("blob envelope");
        (Directory.Exists(BlobRoot) && Directory.EnumerateFiles(BlobRoot, "*", SearchOption.AllDirectories).Any()).ShouldBeFalse();
    }

    [Test]
    public async Task RestoreBlobs_WhenPostgresStore_ShouldResolveAndPut()
    {
        await using var database = await FreshDatabase.CreateAsync(includeSeed: false);
        var input = Path.Combine(_root, "attachments");
        var content = Plain(3000);
        await WriteBlobObjectAsync(input, "attachments/5c4d3e2f", "application/pdf", content, Passphrase);

        var run = await RunAsync(
            ["restore-blobs", "--in", input],
            passphrase: Passphrase,
            settings: new Dictionary<string, string?> { [BlobStoreServices.DbConnectionKey] = database.ConnectionString });

        run.Code.ShouldBe(0, run.Error);
        run.Output.ShouldContain("Postgres");
        await using var store = new PostgresBlobStore(DatabaseServices.PinUtc(database.ConnectionString));
        await ShouldHoldAsync(store, "attachments/5c4d3e2f", "application/pdf", content);
    }

    private async Task<CliRun> RunAsync(
        IReadOnlyList<string> args,
        string? passphrase = null,
        string stdin = "",
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>
        {
            [BlobStoreServices.RootKey] = BlobRoot,
            [BackupTempFiles.TempKey] = TempFolder,
            [BackupPassphrase.Key] = passphrase,
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await BackupCommands.RunAsync(args, configuration, new StringReader(stdin), output, error);
        return new CliRun(code, output.ToString(), error.ToString());
    }

    private async Task WriteEncryptedAsync(string path, byte[] plain, string passphrase)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await using (var encryptor = _cipher.OpenEncryptor(file, passphrase))
        {
            await encryptor.WriteAsync(plain);
        }
    }

    // Writes the object as the attachment mirror lays it out: <folder>/g1/<key>.slbk. Returns its path.
    private async Task<string> WriteBlobObjectAsync(string folder, string key, string contentType, byte[] content, string passphrase)
    {
        using var envelope = new MemoryStream();
        await BlobEnvelope.WriteAsync(envelope, key, contentType, new MemoryStream(content), CancellationToken.None);
        var path = Path.Combine(folder, "g1", key + ".slbk");
        await WriteEncryptedAsync(path, envelope.ToArray(), passphrase);
        return path;
    }

    private static async Task TruncateAsync(string path, int bytes)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Write);
        file.SetLength(file.Length - bytes);
    }

    private static async Task ShouldHoldAsync(Contracts.Blobs.IBlobStore store, string key, string contentType, byte[] content)
    {
        var blob = await store.GetAsync(key, CancellationToken.None);
        blob.ShouldNotBeNull();
        await using var stream = blob.Content;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        copy.ToArray().ShouldBe(content);
        blob.ContentType.ShouldBe(contentType);
    }

    private static byte[] Plain(int size)
    {
        var bytes = new byte[size];
        new Random(size).NextBytes(bytes);
        return bytes;
    }
}
