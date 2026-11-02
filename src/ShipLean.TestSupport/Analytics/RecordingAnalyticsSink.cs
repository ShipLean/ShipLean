using ShipLean.Contracts.Analytics;

namespace ShipLean.TestSupport.Analytics;

// An IAnalyticsSink for one provider that keeps what it was sent. Throws AnalyticsSendException (with FailWith) when told to.
public sealed class RecordingAnalyticsSink(AnalyticsProvider provider) : IAnalyticsSink
{
    public sealed record Call(IReadOnlyList<AnalyticsEvent> Batch, AnalyticsTarget Target);

    private readonly object _gate = new();
    private readonly List<Call> _calls = [];
    private string? _failure;

    public AnalyticsProvider Provider => provider;

    public IReadOnlyList<Call> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public IReadOnlyList<AnalyticsEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls.SelectMany(c => c.Batch)];
            }
        }
    }

    // Makes every following SendAsync throw an AnalyticsSendException with this message; null stops it.
    public void FailWith(string? message)
    {
        lock (_gate)
        {
            _failure = message;
        }
    }

    public Task SendAsync(IReadOnlyList<AnalyticsEvent> batch, AnalyticsTarget target, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_failure is not null)
            {
                throw new AnalyticsSendException(_failure);
            }
            _calls.Add(new Call([.. batch], target));
        }
        return Task.CompletedTask;
    }
}
