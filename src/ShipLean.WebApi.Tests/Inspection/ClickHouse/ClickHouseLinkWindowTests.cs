namespace ShipLean.WebApi.Tests.Inspection.ClickHouse;

// The finder's link queries carry the window themselves: a link made by a record outside the window does not link the run.
[TestFixture(TestName = "ClickHouse link window, SHIP-1623")]
[Category("SHIP-1555")]
public sealed class ClickHouseLinkWindowTests
{
    private static readonly Dictionary<string, string> Tag = new() { ["shiplean.item"] = "SHIP-1" };
    private static readonly Dictionary<string, string> NoTag = [];

    [Test]
    public async Task FindRuns_WhenTheOnlyLinkingRecordIsOutsideTheWindow_DoesNotLinkTheRun()
    {
        await using var scratch = await ScratchExporter.CreateAsync();
        await scratch.AddSpanAsync("s1", 0, 10, "Unset", Tag);
        await scratch.AddSpanAsync("s1", 3000, 10, "Unset", NoTag);
        var finder = scratch.Finder(scratch.Migrator());
        var query = ScratchExporter.Query("SHIP-1");

        var whole = await finder.FindAsync(query, CancellationToken.None);
        var late = await finder.FindAsync(query with { From = query.From.AddMinutes(30) }, CancellationToken.None);

        whole.Runs.Count.ShouldBe(1, whole.Problem);
        late.Reachable.ShouldBeTrue(late.Problem);
        late.Runs.ShouldBeEmpty();
    }
}
