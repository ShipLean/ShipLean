using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Analytics;

namespace ShipLean.Application.Features.WebVitals;

// Drains the web vitals queue and sends batches (100 samples, or 5 seconds after the first) through the sink of the configured
// provider. Readings are lossy by design: a batch that cannot be sent is counted and dropped, never retried. It idles with no timer
// while the queue is empty, reads the settings when samples arrive (cached for 30 seconds, a failed read keeps the last target), and
// discards a batch when there is no target or the switch is off. On stop it drops what is queued and returns promptly.
public sealed class WebVitalsDispatcher : BackgroundService
{
    public const int MaxBatchSize = 100;
    public const string SentCounterName = "shiplean.web_vitals.sent";
    public const string FailedCounterName = "shiplean.web_vitals.failed";

    public static readonly TimeSpan FlushAfter = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan SettingsLifetime = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan WarnEvery = TimeSpan.FromMinutes(1);

    private readonly WebVitalsQueue _queue;
    private readonly IAnalyticsSettingsReader _settings;
    private readonly IReadOnlyList<IWebVitalsSink> _sinks;
    private readonly TimeProvider _time;
    private readonly ILogger<WebVitalsDispatcher> _logger;
    private readonly Counter<long> _sent;
    private readonly Counter<long> _failed;
    private readonly Dictionary<string, DateTimeOffset> _warnedAt = [];

    private AnalyticsTarget? _target;
    private DateTimeOffset? _targetReadAt;

    public WebVitalsDispatcher(
        WebVitalsQueue queue, IAnalyticsSettingsReader settings, IEnumerable<IWebVitalsSink> sinks,
        TimeProvider time, IMeterFactory meterFactory, ILogger<WebVitalsDispatcher> logger)
    {
        _queue = queue;
        _settings = settings;
        _sinks = [.. sinks];
        _time = time;
        _logger = logger;
        var meter = meterFactory.Create(WebVitalsQueue.MeterName);
        _sent = meter.CreateCounter<long>(SentCounterName);
        _failed = meter.CreateCounter<long>(FailedCounterName);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Start without the starter's execution context, so nothing the loop does carries the request or user of whoever started the host.
        Task started;
        using (ExecutionContext.SuppressFlow())
        {
            started = base.StartAsync(cancellationToken);
        }

        return started;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                var batch = await CollectAsync(stoppingToken);
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                await DeliverUnlessStoppedAsync(batch, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping: what is queued is dropped.
        }
    }

    // Reads up to MaxBatchSize samples. The flush timer exists only while this batch is open.
    private async Task<List<WebVitalsSample>> CollectAsync(CancellationToken stoppingToken)
    {
        List<WebVitalsSample> batch = [];
        using var flush = new CancellationTokenSource(FlushAfter, _time);
        using var either = CancellationTokenSource.CreateLinkedTokenSource(flush.Token, stoppingToken);
        try
        {
            while (batch.Count < MaxBatchSize)
            {
                if (_queue.Reader.TryRead(out var next))
                {
                    batch.Add(next);
                }
                else if (!await _queue.Reader.WaitToReadAsync(either.Token))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The five seconds are up, or the host is stopping: send what has been read.
        }
        return batch;
    }

    // Returns as soon as the host stops, whether or not the sink honours the token; a send left behind is observed so a later fault
    // never surfaces as an unobserved task exception.
    private async Task DeliverUnlessStoppedAsync(List<WebVitalsSample> batch, CancellationToken stoppingToken)
    {
        var delivery = DeliverAsync(batch, stoppingToken);
        TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = stoppingToken.Register(() => stopped.TrySetResult());
        if (await Task.WhenAny(delivery, stopped.Task) == delivery)
        {
            await delivery;
            return;
        }
        _logger.LogDebug("Web vitals send abandoned on stop ({Count} samples)", batch.Count);
        _ = ObserveAbandonedAsync(delivery);
    }

    private static async Task ObserveAbandonedAsync(Task abandoned)
    {
        try
        {
            await abandoned;
        }
        catch (Exception)
        {
            // Dropped on stop; nothing to report.
        }
    }

    private async Task DeliverAsync(List<WebVitalsSample> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }
        var target = await GetTargetAsync(cancellationToken);
        if (target is null || !target.SendWebVitals)
        {
            return;
        }
        var provider = ProviderTag(target.Provider);
        var sink = _sinks.FirstOrDefault(s => s.Provider == target.Provider);
        if (sink is null)
        {
            Fail(provider, batch.Count, $"No web vitals sink is registered for {provider}");
            return;
        }

        try
        {
            await sink.SendAsync(batch, target, cancellationToken);
        }
        catch (Exception problem)
        {
            // Once the host is stopping the batch is dropped, whatever the sink threw: nothing may escape the loop.
            if (!cancellationToken.IsCancellationRequested)
            {
                Fail(provider, batch.Count, Describe(problem));
            }
            return;
        }
        _sent.Add(batch.Count, new KeyValuePair<string, object?>("provider", provider));
    }

    // Only the type name, and for the sinks' own AnalyticsSendException its message (they build it without the site key). Never the
    // exception object, its text or its inner exceptions: an HttpRequestException can carry the request URL.
    private static string Describe(Exception problem)
        => problem is AnalyticsSendException ? $"{nameof(AnalyticsSendException)}: {problem.Message}" : problem.GetType().Name;

    // The target is read when samples arrive, and again once it is 30 seconds old. A read that fails keeps the last target.
    private async Task<AnalyticsTarget?> GetTargetAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (_targetReadAt is { } readAt && now - readAt < SettingsLifetime)
        {
            return _target;
        }
        try
        {
            _target = await _settings.GetTargetAsync(cancellationToken);
            _targetReadAt = now;
        }
        catch (Exception problem) when (!cancellationToken.IsCancellationRequested)
        {
            Warn("settings", $"Reading the analytics settings failed: {problem.GetType().Name}");
        }
        return _target;
    }

    private void Fail(string provider, int count, string reason)
    {
        _failed.Add(count, new KeyValuePair<string, object?>("provider", provider));
        Warn(provider, $"Dropped {count} web vitals samples: {reason}");
    }

    // At most one warning a minute per provider. The message never carries the site key.
    private void Warn(string provider, string message)
    {
        var now = _time.GetUtcNow();
        if (_warnedAt.TryGetValue(provider, out var last) && now - last < WarnEvery)
        {
            return;
        }
        _warnedAt[provider] = now;
        _logger.LogWarning("Web vitals delivery to {Provider}: {Message}", provider, message);
    }

    private static string ProviderTag(AnalyticsProvider provider) => provider switch
    {
        AnalyticsProvider.Umami => "umami",
        AnalyticsProvider.PostHog => "posthog",
        _ => "none",
    };
}
