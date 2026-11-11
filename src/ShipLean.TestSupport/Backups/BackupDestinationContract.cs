using System.Text;
using ShipLean.Contracts.Backups;
using Shouldly;

namespace ShipLean.TestSupport.Backups;

// Behaviour every IBackupDestination must have, whatever it writes to. Create may hand every test the same destination
// (one bucket for the class): each test works under its own key root.
[TestFixture]
public abstract class BackupDestinationContract
{
    protected abstract IBackupDestination Create();

    // A destination every call fails on: in memory, Fail on every step; for a real store, say, keys it refuses (a bucket
    // that doesn't exist won't do for S3, where HEAD on it reads as a missing key).
    protected abstract IBackupDestination CreateUnreachable();

    [Test]
    public async Task Put_WhenExistsChecked_ShouldBeTrue()
    {
        var destination = Create();
        var root = Root();

        await destination.PutAsync(root + "a.slbk", Bytes("abc"), CancellationToken.None);

        (await destination.ExistsAsync(root + "a.slbk", CancellationToken.None)).ShouldBeTrue();
        (await destination.ExistsAsync(root + "b.slbk", CancellationToken.None)).ShouldBeFalse();
    }

    [Test]
    public async Task Exists_WhenOnlyALongerKeyExists_ShouldBeFalse()
    {
        var destination = Create();
        var root = Root();

        await destination.PutAsync(root + "a/bc", Bytes("abc"), CancellationToken.None);

        (await destination.ExistsAsync(root + "a/b", CancellationToken.None)).ShouldBeFalse();
    }

    [Test]
    public async Task Delete_WhenExists_ShouldRemove()
    {
        var destination = Create();
        var root = Root();
        await destination.PutAsync(root + "a.slbk", Bytes("abc"), CancellationToken.None);

        await destination.DeleteAsync(root + "a.slbk", CancellationToken.None);

        (await destination.ExistsAsync(root + "a.slbk", CancellationToken.None)).ShouldBeFalse();
        await destination.DeleteAsync(root + "a.slbk", CancellationToken.None);
    }

    [Test]
    public async Task List_WhenPrefixGiven_ShouldReturnOnlyPrefixWithSizes()
    {
        var destination = Create();
        var root = Root();
        await destination.PutAsync(root + "database/one.slbk", Bytes("abc"), CancellationToken.None);
        await destination.PutAsync(root + "database/two.slbk", Bytes("abcde"), CancellationToken.None);
        await destination.PutAsync(root + "attachments/g1/x.slbk", Bytes("a"), CancellationToken.None);

        var listed = await ListAsync(destination, root + "database/");

        listed.OrderBy(o => o.Key, StringComparer.Ordinal).ShouldBe(
            [new BackupObject(root + "database/one.slbk", 3), new BackupObject(root + "database/two.slbk", 5)]);
    }

    [Test]
    public async Task Put_WhenReplacing_ShouldKeepLatest()
    {
        var destination = Create();
        var root = Root();
        await destination.PutAsync(root + "a.slbk", Bytes("one"), CancellationToken.None);

        await destination.PutAsync(root + "a.slbk", Bytes("longer"), CancellationToken.None);

        var listed = await ListAsync(destination, root);

        listed.ShouldBe([new BackupObject(root + "a.slbk", 6)]);
    }

    [Test]
    public async Task Probe_WhenWritable_ShouldBeOk()
    {
        var destination = Create();

        var result = await destination.ProbeAsync(CancellationToken.None);

        result.Ok.ShouldBeTrue(result.Message);
        result.Step.ShouldBeNull();
        (await ListAsync(destination, "")).Select(o => o.Key).ShouldNotContain(key => key.Contains(".shiplean-probe-", StringComparison.Ordinal));
    }

    [Test]
    public async Task Probe_WhenWriteFails_ShouldReportWriteStep()
    {
        var result = await CreateUnreachable().ProbeAsync(CancellationToken.None);

        result.Ok.ShouldBeFalse();
        result.Step.ShouldBe(BackupProbeResult.WriteStep);
        result.Message.ShouldNotBeNullOrWhiteSpace();
    }

    // Callers catch BackupDestinationException and nothing else, so no store's own exception may get out.
    [Test]
    public async Task Put_WhenUnwritable_ShouldThrowDestinationException()
    {
        await Should.ThrowAsync<BackupDestinationException>(
            () => CreateUnreachable().PutAsync(Root() + "a.slbk", Bytes("abc"), CancellationToken.None));
    }

    [Test]
    public async Task Exists_WhenUnreadable_ShouldThrowDestinationException()
    {
        await Should.ThrowAsync<BackupDestinationException>(() => CreateUnreachable().ExistsAsync(Root() + "a.slbk", CancellationToken.None));
    }

    [Test]
    public async Task List_WhenUnreadable_ShouldThrowDestinationException()
    {
        await Should.ThrowAsync<BackupDestinationException>(() => ListAsync(CreateUnreachable(), Root()));
    }

    [Test]
    public async Task Delete_WhenUndeletable_ShouldThrowDestinationException()
    {
        await Should.ThrowAsync<BackupDestinationException>(() => CreateUnreachable().DeleteAsync(Root() + "a.slbk", CancellationToken.None));
    }

    private static async Task<List<BackupObject>> ListAsync(IBackupDestination destination, string prefix)
    {
        var listed = new List<BackupObject>();
        await foreach (var item in destination.ListAsync(prefix, CancellationToken.None))
        {
            listed.Add(item);
        }

        return listed;
    }

    private static string Root() => $"contract/{Guid.NewGuid():N}/";

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));
}
