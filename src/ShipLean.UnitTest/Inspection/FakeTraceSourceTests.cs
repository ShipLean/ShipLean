using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.UnitTest.Inspection;

[TestFixture]
public class FakeTraceSourceTests
{
    private static readonly DateTimeOffset From = TestDates.Today.AddYears(-10);
    private static readonly DateTimeOffset To = TestDates.Today.AddYears(10);

    private static RunQuery Query(string project, string item) => new(project, item, From, To, null, null);

    private static RunRef Ref(string project, string item, string run) => new(project, item, run, From, To, null, null);

    [TestCase("claude-code-synthetic", "sess-cc-1")]
    [TestCase("codex-cli-synthetic", "sess-cx-1")]
    [TestCase("cursor-synthetic", "sess-cu-1")]
    public async Task FindRunsAsync_WhenAnItemTheFixtureIsTaggedWith_ShouldFindTheRun(string name, string run)
    {
        var source = new FakeTraceSource(InspectionFixtures.Synthetic);

        var found = await source.FindRunsAsync(Query("SHIP", "SHIP-1"), CancellationToken.None);

        found.Reachable.ShouldBeTrue();
        found.Runs.Select(r => r.RunId).ShouldContain(run, name);
    }

    [Test]
    public async Task FindRunsAsync_WhenAnItemTheRunIsNotLinkedTo_ShouldFindNothing()
    {
        var source = new FakeTraceSource(InspectionFixtures.Synthetic);

        (await source.FindRunsAsync(Query("SHIP", "SHIP-77"), CancellationToken.None)).Runs.ShouldBeEmpty();
        (await source.GetRunAsync(Ref("SHIP", "SHIP-77", "sess-cc-1"), CancellationToken.None)).ShouldBeNull();
    }

    [Test]
    public async Task GetRunAsync_WhenTheRunLinksToTheItemByAToolCall_ShouldReturnIt()
    {
        var source = new FakeTraceSource(InspectionFixtures.Synthetic);

        var run = await source.GetRunAsync(Ref("SHIP", "SHIP-2", "sess-cc-1"), CancellationToken.None);

        run.ShouldNotBeNull().Summary.Links.Single(l => l.ItemKey == "SHIP-2").Source.ShouldBe(LinkSource.FromToolCall);
    }

    [Test]
    public async Task FindRunsAsync_WhenAnotherProjectOrAWindowBeforeTheRun_ShouldFindNothing()
    {
        var source = new FakeTraceSource(InspectionFixtures.Synthetic);

        (await source.FindRunsAsync(Query("OTHER", "SHIP-1"), CancellationToken.None)).Runs.ShouldBeEmpty();
        (await source.FindRunsAsync(new RunQuery("SHIP", "SHIP-1", From.AddYears(-1), From.AddYears(-1).AddDays(1), null, null), CancellationToken.None)).Runs.ShouldBeEmpty();
    }

    [Test]
    public async Task Remap_WhenPointedAtASeededItem_ShouldFindTheRunUnderTheNewKeysOnly()
    {
        var source = new FakeTraceSource(InspectionFixtures.Synthetic);
        var start = TestDates.Today.AddDays(3);

        source.Remap("claude-code-synthetic", "DEMO", "DEMO-14", start);

        var found = (await source.FindRunsAsync(new RunQuery("DEMO", "DEMO-14", start.AddDays(-1), start.AddDays(1), null, null), CancellationToken.None)).Runs;
        found.ShouldHaveSingleItem().StartedAt.ShouldBe(start);
        (await source.FindRunsAsync(Query("SHIP", "SHIP-1"), CancellationToken.None)).Runs.Select(r => r.RunId).ShouldNotContain("sess-cc-1");
        (await source.GetRunAsync(new RunRef("DEMO", "DEMO-14", "sess-cc-1", start.AddDays(-1), start.AddDays(1), null, null), CancellationToken.None))
            .ShouldNotBeNull().Spans.ShouldAllBe(s => s.Attributes["shiplean.project"] == "DEMO" && s.Attributes["shiplean.item"] == "DEMO-14");
    }

    [Test]
    public async Task Remap_WhenSpanInputNamesTheOldItem_ShouldRewriteItsItemKey()
    {
        var source = new FakeTraceSource(InspectionFixtures.Synthetic);
        var start = TestDates.Today.AddDays(3);

        source.Remap("claude-code-synthetic", "DEMO", "DEMO-14", start);

        var run = (await source.GetRunAsync(new RunRef("DEMO", "DEMO-14", "sess-cc-1", start.AddDays(-1), start.AddDays(1), null, null), CancellationToken.None)).ShouldNotBeNull();
        var inputs = run.Spans.Select(s => s.Attributes.GetValueOrDefault("tool_parameters")).OfType<string>().ToList();
        inputs.ShouldContain("{\"item\":\"DEMO-14\"}");
        inputs.ShouldNotContain(i => i.Contains("SHIP-1\"", StringComparison.Ordinal));
    }
}
