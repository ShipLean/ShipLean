namespace ShipLean.Contracts.Analytics;

// Delivers a batch to one provider. Umami and PostHog each register one.
[MultipleImplementations]
public interface IAnalyticsSink
{
    AnalyticsProvider Provider { get; }

    // Throws AnalyticsSendException on any failure.
    Task SendAsync(IReadOnlyList<AnalyticsEvent> batch, AnalyticsTarget target, CancellationToken cancellationToken);
}
