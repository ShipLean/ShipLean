using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Tests.Taskboard;

namespace ShipLean.WebApi.Tests.Inspection.Views;

// The section reads each run once, a few at a time (never all at once), and keeps the newest-first order.
[TestFixture]
public class SectionParallelReadTests : InspectionViewsFixture
{
    private const int RunCount = 10;

    private SlowSource? slow;

    protected override Action<IServiceCollection>? ConfigureTestServices
    {
        get
        {
            var baseConfigure = base.ConfigureTestServices;
            return services =>
            {
                baseConfigure?.Invoke(services);
                services.RemoveAll<ITraceSource>();
                slow = new SlowSource(Fake, RunId, RunCount);
                services.AddSingleton<ITraceSource>(slow);
            };
        }
    }

    [Test]
    public async Task Section_WhenManyRunsAreLinked_ShouldReadEachOnceAFewAtATimeAndKeepNewestFirst()
    {
        var section = await Api.GetAsync<AgentRunsSectionDto>(RunsUrl);

        section.Rows.Count.ShouldBe(RunCount);
        section.Rows.Select(r => r.RunId).ShouldBe(Enumerable.Range(0, RunCount).Reverse().Select(i => $"run-{i}"));
        slow!.Reads.ShouldBe(RunCount);
        slow.MaxInFlight.ShouldBeInRange(2, 4);
    }

    private sealed class SlowSource(ITraceSource inner, string baseRun, int count) : ITraceSource
    {
        private int inFlight;
        private int reads;
        private int maxInFlight;

        public int Reads => reads;

        public int MaxInFlight => maxInFlight;

        public async Task<RunSearch> FindRunsAsync(RunQuery query, CancellationToken ct)
        {
            var one = (await inner.FindRunsAsync(query, ct)).Runs.Single();
            var runs = Enumerable.Range(0, count).Select(i => one with { RunId = $"run-{i}", StartedAt = one.StartedAt.AddSeconds(i), EndedAt = one.EndedAt.AddSeconds(i) }).ToList();
            return new RunSearch(runs, true, null);
        }

        public async Task<RawRun?> GetRunAsync(RunRef run, CancellationToken ct)
        {
            Interlocked.Increment(ref reads);
            var now = Interlocked.Increment(ref inFlight);
            RaiseMax(now);
            await Task.Delay(50, ct);
            Interlocked.Decrement(ref inFlight);
            var raw = await inner.GetRunAsync(run with { RunId = baseRun }, ct);
            return raw is null ? null : raw with { Summary = raw.Summary with { RunId = run.RunId } };
        }

        private void RaiseMax(int now)
        {
            int seen;
            do
            {
                seen = Volatile.Read(ref maxInFlight);
            }
            while (now > seen && Interlocked.CompareExchange(ref maxInFlight, now, seen) != seen);
        }

        public Task<StoreHealth> CheckAsync(CancellationToken ct) => inner.CheckAsync(ct);

        public Task<IReadOnlyList<FieldCheck>> CheckFieldsAsync(CancellationToken ct) => inner.CheckFieldsAsync(ct);
    }
}
