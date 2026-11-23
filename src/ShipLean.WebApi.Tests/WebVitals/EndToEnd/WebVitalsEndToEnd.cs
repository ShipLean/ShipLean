using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.WebVitals;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport.Analytics;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.WebVitals.EndToEnd;

// The real web vitals pipeline for an end-to-end test: the real queue and dispatcher, with a recording sink for Umami, a settings
// double and every clock on Clock. Register it in ConfigureTestServices; nothing sleeps: AdvanceUntilAsync moves Clock a second at
// a time until a condition holds.
public sealed class WebVitalsEndToEnd
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    public FakeTimeProvider Clock { get; } = new(TimeProvider.System.GetUtcNow());

    public FixedAnalyticsSettingsReader Settings { get; } = new();

    public RecordingWebVitalsSink Sink { get; } = new(AnalyticsProvider.Umami);

    public void Register(IServiceCollection services)
    {
        // Restores the real queue over the host's RecordingWebVitals.
        WebVitalsServices.Register(services);
        services.AddSingleton<TimeProvider>(Clock);
        services.RemoveAll<IWebVitalsSink>();
        services.AddSingleton<IWebVitalsSink>(Sink);
        services.RemoveAll<IAnalyticsSettingsReader>();
        services.AddSingleton<IAnalyticsSettingsReader>(Settings);
    }

    public async Task AdvanceUntilAsync(Func<bool> condition)
    {
        using var limit = new CancellationTokenSource(Limit);
        while (!condition())
        {
            limit.Token.ThrowIfCancellationRequested();
            Clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
    }
}
