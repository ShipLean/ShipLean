using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Analytics;
using ShipLean.Application.Features.Usage;
using ShipLean.Contracts.Analytics;

namespace ShipLean.TestSupport.Analytics;

// The real usage pipeline for an end-to-end delivery test: the real queue, dispatcher, mapper, settings reader and both real sinks,
// with the named "analytics" HttpClient answered by Stub and every clock on Clock. Register it in ConfigureTestServices; nothing sleeps:
// AdvanceUntilAsync moves Clock a second at a time until a condition holds.
public sealed class EndToEndAnalytics : IDisposable
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _reads = new(0);

    public FakeTimeProvider Clock { get; } = new(TimeProvider.System.GetUtcNow());

    public AnalyticsSinkStubHandler Stub { get; } = new();

    // Adds the doubles to the host. The queue is the real IUsageEvents, the sinks and the settings store are the host's own.
    public void Register(IServiceCollection services)
    {
        UsageServices.Register(services);
        services.AddSingleton<TimeProvider>(Clock);
        services.AddHttpClient(AnalyticsServices.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Stub);
        services.RemoveAll<IAnalyticsSettingsReader>();
        services.AddSingleton<IAnalyticsSettingsReader>(sp => new CountingReader(ActivatorUtilities.CreateInstance<AnalyticsSettingsReader>(sp), _reads));
    }

    // Waits for the settings reader to be called once more than it had (the dispatcher, or the settings page).
    public Task ReadSettingsAsync() => AdvanceUntilSignalAsync(_reads, "the settings reader to be called");

    // Advances the clock until the semaphore is released; SemaphoreSlim.WaitAsync(timeout) returns false instead of throwing, so check it.
    public async Task AdvanceUntilSignalAsync(SemaphoreSlim signal, string what)
    {
        var wait = signal.WaitAsync(Limit);
        await AdvanceUntilAsync(wait);
        if (!await wait)
        {
            throw new TimeoutException($"Timed out after {Limit.TotalSeconds:0} s waiting for {what}.");
        }
    }

    // The dispatcher caches the target for SettingsLifetime: a test that changes the settings moves the clock past that first.
    public void ExpireSettingsCache() => Clock.Advance(UsageDispatcher.SettingsLifetime + TimeSpan.FromSeconds(1));

    public async Task AdvanceUntilAsync(Task done)
    {
        using var limit = new CancellationTokenSource(Limit);
        while (!done.IsCompleted)
        {
            limit.Token.ThrowIfCancellationRequested();
            Clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
        await done;
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

    public void Dispose()
    {
        _reads.Dispose();
        Stub.Dispose();
    }

    private sealed class CountingReader(IAnalyticsSettingsReader inner, SemaphoreSlim reads) : IAnalyticsSettingsReader
    {
        public async Task<AnalyticsTarget?> GetTargetAsync(CancellationToken cancellationToken)
        {
            var target = await inner.GetTargetAsync(cancellationToken);
            reads.Release();
            return target;
        }
    }
}
