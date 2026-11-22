using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.Analytics.Umami;

// Sends web vitals readings to Umami as performance items.
public sealed class UmamiWebVitalsSink : IWebVitalsSink
{
    public AnalyticsProvider Provider => AnalyticsProvider.Umami;

    // TODO: the real send to Umami's /api/batch replaces this stub.
    public Task SendAsync(IReadOnlyList<WebVitalsSample> batch, AnalyticsTarget target, CancellationToken cancellationToken)
        => throw new AnalyticsSendException("Sending web vitals to Umami is not built yet.");
}
