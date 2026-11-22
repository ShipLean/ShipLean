namespace ShipLean.Contracts.Analytics;

// Delivers a batch of web vitals readings to one provider. Umami and PostHog each register one. Separate from IAnalyticsSink, so a
// bad reading never fails a batch of usage events.
[MultipleImplementations]
public interface IWebVitalsSink
{
    AnalyticsProvider Provider { get; }

    // Throws AnalyticsSendException on any failure.
    Task SendAsync(IReadOnlyList<WebVitalsSample> batch, AnalyticsTarget target, CancellationToken cancellationToken);
}
