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

    // A destination whose writes fail: in memory, Fail("write"); for a real store, say, a bucket that doesn't exist.
    protected abstract IBackupDestination CreateUnwritable();

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

        var listed = new List<BackupObject>();
        await foreach (var item in destination.ListAsync(root + "database/", CancellationToken.None))
        {
            listed.Add(item);
        }

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

        var listed = new List<BackupObject>();
        await foreach (var item in destination.ListAsync(root, CancellationToken.None))
        {
            listed.Add(item);
        }

        listed.ShouldBe([new BackupObject(root + "a.slbk", 6)]);
    }

    [Test]
    public async Task Probe_WhenWritable_ShouldBeOk()
    {
        var result = await Create().ProbeAsync(CancellationToken.None);

        result.Ok.ShouldBeTrue(result.Message);
        result.Step.ShouldBeNull();
    }

    [Test]
    public async Task Probe_WhenWriteFails_ShouldReportWriteStep()
    {
        var result = await CreateUnwritable().ProbeAsync(CancellationToken.None);

        result.Ok.ShouldBeFalse();
        result.Step.ShouldBe(BackupProbeResult.WriteStep);
        result.Message.ShouldNotBeNullOrWhiteSpace();
    }

    private static string Root() => $"contract/{Guid.NewGuid():N}/";

    private static MemoryStream Bytes(string text) => new(Encoding.UTF8.GetBytes(text));
}
