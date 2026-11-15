using ShipLean.Application.Features.Inspection;
using ShipLean.Contracts.Inspection;
using ShipLean.LocalEnvironment.Inspection;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Contract;

// What every ITraceSource must do over the synthetic fixtures (K7), loaded through the collector into the shared stack. A subclass
// supplies the source under test, built directly from the stack's options (no DI).
public abstract class TraceSourceContractTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(StoreTimeouts.WaitSeconds);
    private readonly Dictionary<string, LoadedRun> runs = [];
    private ITraceSource source = null!;

    public static IEnumerable<string> FixtureNames => InspectionFixtures.Synthetic.Select(f => f.Name);

    protected abstract string Store { get; }

    // The key of the lane whose work turns this suite green; it leads every assertion message.
    protected abstract string Lane { get; }

    protected abstract ITraceSource CreateSource(InspectionOptions options);

    [OneTimeSetUp]
    public async Task LoadFixtures()
    {
        var stack = await InspectionStack.GetAsync();
        source = CreateSource(stack.Options(Store));
        foreach (var fixture in InspectionFixtures.Synthetic)
        {
            runs[fixture.Name] = await LoadedRun.LoadAsync(stack, fixture, fixture.Name == "claude-code-synthetic" ? 2 : 1, Timeout);
        }
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task FindRuns_WhenTheRunIsLinked_ReturnsItsSummaryAndLinks(string name)
    {
        var run = runs[name];

        var search = await source.FindRunsAsync(Query(run, run.ItemKey), CancellationToken.None);

        search.Reachable.ShouldBeTrue($"{Lane}: {search.Problem}");
        var found = search.Runs.Where(r => r.RunId == run.Expected.Summary.RunId).ToList();
        found.Count.ShouldBe(1, $"{Lane}: runs found: {string.Join(", ", search.Runs.Select(r => r.RunId))}");
        var expected = run.Expected with { Spans = [], Logs = [] };
        RawRunComparer.Differences(expected, new RawRun(found[0], [], [], expected.Truncated), run.Fixture.CompareKeys).ShouldBeEmpty(Lane);
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task FindRunsAndGetRun_WhenAskedUnderEachLinkedItem_ServeTheRun(string name)
    {
        var run = runs[name];

        foreach (var link in run.Expected.Summary.Links)
        {
            var search = await source.FindRunsAsync(Query(run, link.ItemKey), CancellationToken.None);
            search.Runs.Select(r => r.RunId).ShouldContain(run.Expected.Summary.RunId, $"{Lane}: find under {link.ItemKey}");
            (await source.GetRunAsync(Ref(run, link.ItemKey, run.Expected.Summary.RunId), CancellationToken.None)).ShouldNotBeNull($"{Lane}: get under {link.ItemKey}");
        }
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task GetRun_WhenTheRunExists_EqualsTheExpectedRun(string name)
    {
        var run = runs[name];

        var actual = await source.GetRunAsync(Ref(run, run.ItemKey, run.Expected.Summary.RunId), CancellationToken.None);

        actual.ShouldNotBeNull(Lane);
        RawRunComparer.Differences(run.Expected, actual, run.Fixture.CompareKeys).ShouldBeEmpty(Lane);
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task FindRunsAndGetRun_WhenAnotherProjectKeyIsUsed_FindNothing(string name)
    {
        var run = runs[name];
        var other = run.ProjectKey + "X";

        var search = await source.FindRunsAsync(Query(run, run.ItemKey) with { ProjectKey = other }, CancellationToken.None);
        var got = await source.GetRunAsync(Ref(run, run.ItemKey, run.Expected.Summary.RunId) with { ProjectKey = other }, CancellationToken.None);

        search.Reachable.ShouldBeTrue($"{Lane}: {search.Problem}");
        search.Runs.ShouldBeEmpty(Lane);
        got.ShouldBeNull(Lane);
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task FindRunsAndGetRun_WhenTheWindowIsBeforeTheRun_FindNothing(string name)
    {
        var run = runs[name];
        var (from, to) = (run.Load.StartedAt.AddHours(-3), run.Load.StartedAt.AddHours(-2));

        var search = await source.FindRunsAsync(Query(run, run.ItemKey) with { From = from, To = to }, CancellationToken.None);
        var got = await source.GetRunAsync(Ref(run, run.ItemKey, run.Expected.Summary.RunId) with { From = from, To = to }, CancellationToken.None);

        search.Reachable.ShouldBeTrue($"{Lane}: {search.Problem}");
        search.Runs.ShouldBeEmpty(Lane);
        got.ShouldBeNull(Lane);
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task GetRun_WhenTheRunIsUnknown_IsNull(string name)
    {
        var run = runs[name];

        var got = await source.GetRunAsync(Ref(run, run.ItemKey, "no-such-session"), CancellationToken.None);

        got.ShouldBeNull(Lane);
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task FindRunsAndGetRun_WhenTheItemIsNotLinked_FindNothing(string name)
    {
        var run = runs[name];
        const string unlinked = "SHIP-999999";

        var search = await source.FindRunsAsync(Query(run, unlinked), CancellationToken.None);
        var got = await source.GetRunAsync(Ref(run, unlinked, run.Expected.Summary.RunId), CancellationToken.None);

        search.Reachable.ShouldBeTrue($"{Lane}: {search.Problem}");
        search.Runs.ShouldBeEmpty(Lane);
        got.ShouldBeNull(Lane);
    }

    private static RunQuery Query(LoadedRun run, string itemKey)
        => new(run.ProjectKey, itemKey, run.Load.StartedAt.AddMinutes(-1), run.Load.StartedAt.AddHours(1), null, null);

    private static RunRef Ref(LoadedRun run, string itemKey, string runId)
        => new(run.ProjectKey, itemKey, runId, run.Load.StartedAt.AddMinutes(-1), run.Load.StartedAt.AddHours(1), null, null);
}
