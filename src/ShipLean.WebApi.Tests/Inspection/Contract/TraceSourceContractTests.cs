using ShipLean.Application.Features.Inspection;
using ShipLean.Contracts.Inspection;
using ShipLean.LocalEnvironment.Inspection;
using ShipLean.TestSupport.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.Contract;

// What every ITraceSource must do over the synthetic fixtures (K7), loaded through the collector into the shared stack. A subclass
// supplies the source under test, built directly from the stack's options (no DI). Lane A must talk to ClickHouse over the HTTP
// interface (port 8123 inside the container) of the shared stack, not the native protocol.
public abstract class TraceSourceContractTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(StoreTimeouts.WaitSeconds);
    private readonly Dictionary<string, LoadedRun> runs = [];
    private ITraceSource source = null!;

    // Every fixture a lane may run: the synthetic ones and the recorded ones. A suite lists the recorded names it supports in RecordedNames.
    public static IEnumerable<string> FixtureNames => InspectionFixtures.Synthetic.Concat(InspectionFixtures.Recorded).Select(f => f.Name);

    // The recorded fixtures this store has been opted into (D27: only real agent runs exist as recordings). A store not listed skips them.
    protected virtual IReadOnlyCollection<string> RecordedNames => [];

    protected abstract string Store { get; }

    // The key of the lane whose work turns this suite green; it leads every assertion message.
    protected abstract string Lane { get; }

    protected abstract ITraceSource CreateSource(InspectionOptions options);

    [OneTimeSetUp]
    public async Task LoadFixtures()
    {
        var stack = await InspectionStack.GetAsync();
        source = CreateSource(stack.Options(Store));
        foreach (var fixture in InspectionFixtures.Synthetic.Concat(InspectionFixtures.Recorded.Where(f => RecordedNames.Contains(f.Name))))
        {
            runs[fixture.Name] = await LoadedRun.LoadAsync(stack, fixture, fixture.Name == "claude-code-synthetic" ? 2 : 1, Timeout);
        }
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task FindRuns_WhenTheRunIsLinked_ReturnsItsSummaryAndLinks(string name)
    {
        var run = Loaded(name);

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
        var run = Loaded(name);

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
        var run = Loaded(name);

        var actual = await source.GetRunAsync(Ref(run, run.ItemKey, run.Expected.Summary.RunId), CancellationToken.None);

        actual.ShouldNotBeNull(Lane);
        RawRunComparer.Differences(run.Expected, actual, run.Fixture.CompareKeys).ShouldBeEmpty(Lane);
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task FindRunsAndGetRun_WhenAnotherProjectKeyIsUsed_FindNothing(string name)
    {
        var run = Loaded(name);
        await AssertFoundAsync(run);

        // Longer than the real key (catches a store matching stored.StartsWith(query) in reverse), a prefix of it (catches a prefix match), and unrelated.
        foreach (var other in new[] { run.ProjectKey + "X", run.ProjectKey[..^1], "ZZZZ-NOPE" })
        {
            var search = await source.FindRunsAsync(Query(run, run.ItemKey) with { ProjectKey = other }, CancellationToken.None);
            var got = await source.GetRunAsync(Ref(run, run.ItemKey, run.Expected.Summary.RunId) with { ProjectKey = other }, CancellationToken.None);

            search.Reachable.ShouldBeTrue($"{Lane}: {search.Problem}");
            search.Runs.ShouldBeEmpty($"{Lane}: project {other}");
            got.ShouldBeNull($"{Lane}: project {other}");
        }
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task FindRunsAndGetRun_WhenTheWindowIsBeforeTheRun_FindNothing(string name)
    {
        var run = Loaded(name);
        var (from, to) = (run.Load.StartedAt.AddHours(-3), run.Load.StartedAt.AddHours(-2));
        await AssertFoundAsync(run);

        var search = await source.FindRunsAsync(Query(run, run.ItemKey) with { From = from, To = to }, CancellationToken.None);
        var got = await source.GetRunAsync(Ref(run, run.ItemKey, run.Expected.Summary.RunId) with { From = from, To = to }, CancellationToken.None);

        search.Reachable.ShouldBeTrue($"{Lane}: {search.Problem}");
        search.Runs.ShouldBeEmpty(Lane);
        got.ShouldBeNull(Lane);
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task GetRun_WhenTheRunIsUnknown_IsNull(string name)
    {
        var run = Loaded(name);
        await AssertFoundAsync(run);

        var got = await source.GetRunAsync(Ref(run, run.ItemKey, "no-such-session"), CancellationToken.None);

        got.ShouldBeNull(Lane);
    }

    [TestCaseSource(nameof(FixtureNames))]
    public async Task FindRunsAndGetRun_WhenTheItemIsNotLinked_FindNothing(string name)
    {
        var run = Loaded(name);
        const string unlinked = "SHIP-999999";
        await AssertFoundAsync(run);

        var search = await source.FindRunsAsync(Query(run, unlinked), CancellationToken.None);
        var got = await source.GetRunAsync(Ref(run, unlinked, run.Expected.Summary.RunId), CancellationToken.None);

        search.Reachable.ShouldBeTrue($"{Lane}: {search.Problem}");
        search.Runs.ShouldBeEmpty(Lane);
        got.ShouldBeNull(Lane);
    }

    private LoadedRun Loaded(string name)
    {
        Assume.That(runs.ContainsKey(name), $"{name} is not opted into the {Store} suite yet.");
        return runs[name];
    }

    // The positive control for every negative test: the correct query and ref DO find the run, so a store that returns nothing fails.
    private async Task AssertFoundAsync(LoadedRun run)
    {
        var search = await source.FindRunsAsync(Query(run, run.ItemKey), CancellationToken.None);
        search.Runs.Select(r => r.RunId).ShouldContain(run.Expected.Summary.RunId, $"{Lane}: positive control, find");
        (await source.GetRunAsync(Ref(run, run.ItemKey, run.Expected.Summary.RunId), CancellationToken.None)).ShouldNotBeNull($"{Lane}: positive control, get");
    }

    private static RunQuery Query(LoadedRun run, string itemKey)
        => new(run.ProjectKey, itemKey, run.Load.StartedAt.AddMinutes(-1), run.Load.StartedAt.AddHours(1), null, null);

    private static RunRef Ref(LoadedRun run, string itemKey, string runId)
        => new(run.ProjectKey, itemKey, runId, run.Load.StartedAt.AddMinutes(-1), run.Load.StartedAt.AddHours(1), null, null);
}
