using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Configuration;
using ShipLean.Application.Fakes.Backups.Engine;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Mirror;
using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Backups.Mirror;

// SHIP-791: the attachment mirror, over in-memory doubles for the store, inventory, destination and cipher.
[TestFixture]
public class AttachmentMirrorTests
{
    private const string Prefix = "backups/";
    private const string Passphrase = "correct horse";

    private string _folder = null!;
    private InMemoryBlobStore _blobs = null!;
    private InMemoryBackupDestination _destination = null!;
    private PassThroughBackupCipher _cipher = null!;

    [SetUp]
    public void CreateDoubles()
    {
        _folder = Directory.CreateTempSubdirectory("shiplean-mirror-tests-").FullName;
        _blobs = new InMemoryBlobStore();
        _destination = new InMemoryBackupDestination();
        _cipher = new PassThroughBackupCipher();
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(_folder, recursive: true);

    [Test]
    public async Task Mirror_WhenNothingCopied_ShouldCopyAll()
    {
        await PutAsync("attachments/a", "alpha", "text/plain");
        await PutAsync("attachments/b", "bravo!", "image/png");
        await PutAsync("avatars/u/c", "charlie", "image/webp");

        var result = await MirrorAsync(new InMemoryBlobInventory(_blobs), 1);

        result.Copied.ShouldBe(3);
        var stored = await _destination.ListAsync(Prefix, CancellationToken.None).ToListAsync();
        stored.Select(o => o.Key).ShouldBe(
            new[] { "attachments/a", "attachments/b", "avatars/u/c" }.Select(k => BackupObjectKeys.Attachment(Prefix, 1, k)).Order(StringComparer.Ordinal));
        result.Bytes.ShouldBe(stored.Sum(o => o.Size));
    }

    [Test]
    public async Task Mirror_WhenRunTwice_ShouldCopyOnlyNew()
    {
        await PutAsync("attachments/a", "alpha", "text/plain");
        await PutAsync("attachments/b", "bravo", "text/plain");
        var inventory = new InMemoryBlobInventory(_blobs);
        (await MirrorAsync(inventory, 1)).Copied.ShouldBe(2);

        var again = await MirrorAsync(inventory, 1);
        again.ShouldBe(new MirrorResult(0, 0));

        await PutAsync("attachments/c", "charlie", "text/plain");
        (await MirrorAsync(inventory, 1)).Copied.ShouldBe(1);
    }

    [Test]
    public async Task Mirror_WhenRunTwice_ShouldNotCheckEachKey()
    {
        await PutAsync("attachments/a", "alpha", "text/plain");
        await PutAsync("attachments/b", "bravo", "text/plain");
        var inventory = new InMemoryBlobInventory(_blobs);
        await MirrorAsync(inventory, 1);
        var counting = new CountingDestination(_destination);

        var again = await MirrorAsync(inventory, 1, counting);

        again.ShouldBe(new MirrorResult(0, 0));
        counting.ExistsCalls.ShouldBe(0);
        counting.ListCalls.ShouldBe(1);
    }

    [Test]
    public async Task Mirror_WhenGenerationChanges_ShouldCopyAllAgain()
    {
        await PutAsync("attachments/a", "alpha", "text/plain");
        await PutAsync("attachments/b", "bravo", "text/plain");
        var inventory = new InMemoryBlobInventory(_blobs);
        await MirrorAsync(inventory, 1);

        var next = await MirrorAsync(inventory, 2);

        next.Copied.ShouldBe(2);
        (await _destination.ExistsAsync(BackupObjectKeys.Attachment(Prefix, 2, "attachments/a"), CancellationToken.None)).ShouldBeTrue();
    }

    [Test]
    public async Task Mirror_WhenListedBlobMissing_ShouldThrowNamingKey()
    {
        await PutAsync("attachments/a", "alpha", "text/plain");

        var thrown = await Should.ThrowAsync<BlobUnreadableException>(() => MirrorAsync(new ListedInventory("attachments/a", "attachments/gone"), 1));

        thrown.BlobKey.ShouldBe("attachments/gone");
        thrown.Message.ShouldBe("Blob 'attachments/gone' is listed but can't be read.");
    }

    [Test]
    public async Task Mirror_WhenDestinationFails_ShouldThrow()
    {
        await PutAsync("attachments/a", "alpha", "text/plain");
        _destination.Fail(BackupProbeResult.WriteStep);

        await Should.ThrowAsync<BackupDestinationException>(() => MirrorAsync(new InMemoryBlobInventory(_blobs), 1));
    }

    [Test]
    public async Task Mirror_WhenDone_ShouldLeaveNoTempFiles()
    {
        await PutAsync("attachments/a", "alpha", "text/plain");
        await PutAsync("attachments/b", "bravo", "text/plain");
        await MirrorAsync(new InMemoryBlobInventory(_blobs), 1);
        Directory.GetFiles(_folder).ShouldBeEmpty();

        _destination.Fail(BackupProbeResult.WriteStep);
        await Should.ThrowAsync<BackupDestinationException>(() => MirrorAsync(new InMemoryBlobInventory(_blobs), 2));

        Directory.GetFiles(_folder).ShouldBeEmpty();
    }

    [Test]
    public async Task Mirror_WhenCopied_ShouldKeepContentType()
    {
        await PutAsync("attachments/a", "alpha content", "image/svg+xml");

        await MirrorAsync(new InMemoryBlobInventory(_blobs), 1);

        using var encrypted = new MemoryStream(_destination.Read(BackupObjectKeys.Attachment(Prefix, 1, "attachments/a")));
        using var plain = new MemoryStream();
        await _cipher.DecryptAsync(encrypted, plain, Passphrase, CancellationToken.None);
        plain.Position = 0;
        var (key, contentType, content) = await BlobEnvelope.ReadAsync(plain);
        key.ShouldBe("attachments/a");
        contentType.ShouldBe("image/svg+xml");
        using var reader = new StreamReader(content, Encoding.UTF8);
        (await reader.ReadToEndAsync()).ShouldBe("alpha content");
    }

    private Task PutAsync(string key, string text, string contentType)
        => _blobs.PutAsync(key, new MemoryStream(Encoding.UTF8.GetBytes(text)), contentType, CancellationToken.None);

    private Task<MirrorResult> MirrorAsync(IBlobInventory inventory, int generation, IBackupDestination? destination = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [BackupTempFiles.TempKey] = _folder })
            .Build();
        var mirror = new AttachmentMirror(inventory, _blobs, _cipher, new BackupTempFiles(configuration));
        return mirror.MirrorAsync(destination ?? _destination, Prefix, generation, Passphrase, CancellationToken.None);
    }

    // Lists the given keys whether or not the store holds them.
    private sealed class ListedInventory(params string[] keys) : IBlobInventory
    {
        public async IAsyncEnumerable<string> ListKeysAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var key in keys)
            {
                yield return key;
            }

            await Task.CompletedTask;
        }
    }

    // Forwards to the in-memory destination and counts the calls the mirror makes to check what it holds.
    private sealed class CountingDestination(InMemoryBackupDestination inner) : IBackupDestination
    {
        public int ExistsCalls { get; private set; }

        public int ListCalls { get; private set; }

        public Task PutAsync(string key, Stream content, CancellationToken cancellationToken) => inner.PutAsync(key, content, cancellationToken);

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
        {
            ExistsCalls++;
            return inner.ExistsAsync(key, cancellationToken);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken) => inner.DeleteAsync(key, cancellationToken);

        public IAsyncEnumerable<BackupObject> ListAsync(string prefix, CancellationToken cancellationToken)
        {
            ListCalls++;
            return inner.ListAsync(prefix, cancellationToken);
        }

        public Task<BackupProbeResult> ProbeAsync(CancellationToken cancellationToken) => inner.ProbeAsync(cancellationToken);
    }
}
