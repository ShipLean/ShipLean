using ShipLean.LocalEnvironment.Inspection;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Contract;

// The harness itself: what the collector was sent is what each store holds. The loader waits on query results, never a sleep.
[TestFixture]
public sealed class InspectionStackSelfTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(StoreTimeouts.WaitSeconds);

    public static IEnumerable<string> FixtureNames => InspectionFixtures.Synthetic.Select(f => f.Name);

    [TestCaseSource(nameof(FixtureNames))]
    public async Task LoadedFixture_WhenSentOnce_IsVisibleInClickHouseTempoAndLoki(string name)
    {
        var stack = await InspectionStack.GetAsync();
        var fixture = InspectionFixtures.Get(name);

        var run = await LoadedRun.LoadAsync(stack, fixture, 1, Timeout);

        var (spans, logs) = (fixture.Expected.Summary.SpanCount, fixture.Expected.Summary.LogCount);
        var counts = await stack.WaitUntilVisibleAsync(run.Load, spans, logs, Timeout);
        counts.ShouldBe(new StoreCounts(spans, logs, spans, logs));
    }

    [Test]
    public async Task LoadedFixture_WhenSentInTwoBatches_LandsEveryRow()
    {
        var stack = await InspectionStack.GetAsync();
        var fixture = InspectionFixtures.Get("claude-code-synthetic");

        var run = await LoadedRun.LoadAsync(stack, fixture, 2, Timeout);

        var (spans, logs) = (fixture.Expected.Summary.SpanCount, fixture.Expected.Summary.LogCount);
        (spans, logs).ShouldBe((16, 7));
        var counts = await stack.WaitUntilVisibleAsync(run.Load, spans, logs, Timeout);
        counts.ShouldBe(new StoreCounts(spans, logs, spans, logs));
    }

    [Test]
    public async Task LoadedFixture_WhenLoadedUnderTwoProjectKeys_DoesNotMerge()
    {
        var stack = await InspectionStack.GetAsync();
        var fixture = InspectionFixtures.Get("claude-code-synthetic");

        var first = await LoadedRun.LoadAsync(stack, fixture, 1, Timeout);
        var second = await LoadedRun.LoadAsync(stack, fixture, 1, Timeout);

        first.ProjectKey.ShouldNotBe(second.ProjectKey);
        first.Load.TraceIds.Values.ShouldNotBe(second.Load.TraceIds.Values);
        (await stack.WaitUntilVisibleAsync(first.Load, 16, 7, Timeout)).ShouldBe(new StoreCounts(16, 7, 16, 7));
    }

    [Test]
    public void Compose_WhenComparedWithTheStack_UsesTheSameImageTags()
    {
        var compose = File.ReadAllText(Path.Combine(ShipLean.LocalEnvironment.RepoLayout.Root, "src", "ShipLean.LocalEnvironment", "Inspection", "docker-compose.yml"));
        foreach (var image in new[] { InspectionStack.CollectorImage, InspectionStack.ClickHouseImage, InspectionStack.TempoImage, InspectionStack.LokiImage })
        {
            compose.ShouldContain($"image: {image}");
        }
    }
}
