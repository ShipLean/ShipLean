using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.PostHog;

// Sends web vitals readings to PostHog as $web_vitals events.
public sealed class PostHogWebVitalsSink : IWebVitalsSink
{
    public AnalyticsProvider Provider => AnalyticsProvider.PostHog;

    // TODO: the real send to PostHog's /batch/ replaces this stub.
    public Task SendAsync(IReadOnlyList<WebVitalsSample> batch, AnalyticsTarget target, CancellationToken cancellationToken)
        => throw new AnalyticsSendException("Sending web vitals to PostHog is not built yet.");
}
