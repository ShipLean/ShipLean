using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport.Analytics;

namespace ShipLean.WebApi.Tests.Usage.Delivery;

// A RecordingAnalyticsSink that also says when an attempt starts (Attempts, one release per attempt, failing or not) and can hold
// every attempt on Gate until a test opens it.
internal sealed class ProbeSink(AnalyticsProvider provider) : IAnalyticsSink
{
    private int _attemptCount;

    public RecordingAnalyticsSink Inner { get; } = new(provider);

    public SemaphoreSlim Attempts { get; } = new(0);

    public int AttemptCount => Volatile.Read(ref _attemptCount);

    public TaskCompletionSource? Gate { get; set; }

    public AnalyticsProvider Provider => provider;

    public async Task SendAsync(IReadOnlyList<AnalyticsEvent> batch, AnalyticsTarget target, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _attemptCount);
        Attempts.Release();
        if (Gate is { } gate)
        {
            await gate.Task;
        }
        await Inner.SendAsync(batch, target, cancellationToken);
    }
}
