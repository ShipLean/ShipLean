using System.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// Drains the usage queue, maps each event, batches them (100 events or 2 seconds, whichever comes first) and sends the batch through
// the sink of the configured provider. It is the only reader of the queue, so Track never waits on it, and nothing here throws out of
// the loop: a batch that cannot be sent is retried once after 5 seconds, then dropped and counted. It idles with no timer while the
// queue is empty, reads the settings only when events arrive (and at most every 30 seconds), and on stop sends what is queued
// within 5 seconds.
public sealed class UsageDispatcher : BackgroundService
{
    public const int MaxBatchSize = 100;
    public const string SentCounterName = "shiplean.analytics.sent";
    public const string FailedCounterName = "shiplean.analytics.failed";

    public static readonly TimeSpan FlushAfter = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan SettingsLifetime = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StopCap = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan WarnEvery = TimeSpan.FromMinutes(1);

    private readonly UsageQueue _queue;
    private readonly UsageEventMapper _mapper;
    private readonly IAnalyticsSettingsReader _settings;
    private readonly IReadOnlyList<IAnalyticsSink> _sinks;
    private readonly TimeProvider _time;
    private readonly ILogger<UsageDispatcher> _logger;
    private readonly Counter<long> _sent;
    private readonly Counter<long> _failed;
    private readonly Dictionary<string, DateTimeOffset> _warnedAt = [];

    private AnalyticsTarget? _target;
    private DateTimeOffset? _targetReadAt;

    public UsageDispatcher(
        UsageQueue queue, UsageEventMapper mapper, IAnalyticsSettingsReader settings, IEnumerable<IAnalyticsSink> sinks,
        TimeProvider time, IMeterFactory meterFactory, ILogger<UsageDispatcher> logger)
    {
        _queue = queue;
        _mapper = mapper;
        _settings = settings;
        _sinks = [.. sinks];
        _time = time;
        _logger = logger;
        var meter = meterFactory.Create(UsageQueue.MeterName);
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
        List<UsageEvent> unsent = [];
        try
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                var batch = await CollectAsync(stoppingToken);
                if (stoppingToken.IsCancellationRequested)
                {
                    unsent = batch;
                    break;
                }
                if (!await DeliverUnlessStuckAsync(batch, stoppingToken))
                {
                    // The sink did not return within the stop cap of the host stopping: leave it, and send nothing more.
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
        await FlushOnStopAsync(unsent);
    }

    // Reads up to MaxBatchSize events. The flush timer exists only while this batch is open, and ends the wait for more events.
    private async Task<List<UsageEvent>> CollectAsync(CancellationToken stoppingToken)
    {
        List<UsageEvent> batch = [];
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
            // The two seconds are up, or the host is stopping: send what has been read.
        }
        return batch;
    }

    // Delivers the batch. When the host stops while the sink is still inside SendAsync, waits at most StopCap more for it, whether or
    // not the sink honours the token. False when it was left behind.
    private async Task<bool> DeliverUnlessStuckAsync(List<UsageEvent> batch, CancellationToken stoppingToken)
    {
        var delivery = DeliverAsync(batch, retry: true, stoppingToken);
        TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = stoppingToken.Register(() => stopped.TrySetResult());
        if (await Task.WhenAny(delivery, stopped.Task) == delivery)
        {
            await delivery;
            return true;
        }
        using var giveUp = new CancellationTokenSource();
        var grace = Task.Delay(StopCap, _time, giveUp.Token);
        if (await Task.WhenAny(delivery, grace) != delivery)
        {
            // Abandoned: if the sink faults later, observe it so it never surfaces as an unobserved task exception.
            _ = ObserveAbandonedAsync(delivery);
            return false;
        }
        await giveUp.CancelAsync();
        await delivery;
        return true;
    }

    // Awaits a send nobody waits for any more, so that a later fault is observed. Only the exception type is logged.
    private async Task ObserveAbandonedAsync(Task abandoned)
    {
        try
        {
            await abandoned;
        }
        catch (Exception problem)
        {
            Warn("stop", $"An abandoned usage send failed: {problem.GetType().Name}");
        }
    }

    private async Task FlushOnStopAsync(List<UsageEvent> unsent)
    {
        using var cap = new CancellationTokenSource(StopCap, _time);
        using var giveUp = new CancellationTokenSource();
        var work = DeliverUnsentAsync(unsent, cap.Token);
        // A sink that ignores its token must not hold the stop: stop waiting at the cap whatever the sink does.
        var timeout = Task.Delay(StopCap, _time, giveUp.Token);
        if (await Task.WhenAny(work, timeout) == work)
        {
            await giveUp.CancelAsync();
        }
        else
        {
            await cap.CancelAsync();
        }
    }

    private async Task DeliverUnsentAsync(List<UsageEvent> unsent, CancellationToken cancellationToken)
    {
        try
        {
            while (_queue.Reader.TryRead(out var next))
            {
                unsent.Add(next);
            }
            foreach (var batch in unsent.Chunk(MaxBatchSize))
            {
                await DeliverAsync([.. batch], retry: false, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Out of time: the rest is dropped.
        }
        catch (Exception problem)
        {
            Warn("stop", $"Sending the last usage events failed: {problem.GetType().Name}");
        }
    }

    private async Task DeliverAsync(List<UsageEvent> batch, bool retry, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }
        var target = await GetTargetAsync(cancellationToken);
        if (target is null)
        {
            return;
        }
        var provider = ProviderTag(target.Provider);
        var sink = _sinks.FirstOrDefault(s => s.Provider == target.Provider);
        if (sink is null)
        {
            Fail(provider, batch.Count, $"No sink is registered for {provider}");
            return;
        }

        string? failure;
        try
        {
            List<AnalyticsEvent> mapped = [];
            foreach (var usageEvent in batch)
            {
                mapped.Add(await _mapper.MapAsync(usageEvent, target, cancellationToken));
            }
            failure = await SendAsync(sink, mapped, target, retry, cancellationToken);
        }
        catch (Exception problem) when (!cancellationToken.IsCancellationRequested)
        {
            failure = $"Mapping a batch failed: {problem.GetType().Name}";
        }

        if (failure is null)
        {
            _sent.Add(batch.Count, new KeyValuePair<string, object?>("provider", provider));
        }
        else
        {
            Fail(provider, batch.Count, failure);
        }
    }

    // Null when the batch went; otherwise why it did not, after the one retry.
    private async Task<string?> SendAsync(IAnalyticsSink sink, List<AnalyticsEvent> mapped, AnalyticsTarget target, bool retry, CancellationToken cancellationToken)
    {
        var attempts = retry ? 2 : 1;
        string? failure = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await sink.SendAsync(mapped, target, cancellationToken);
                return null;
            }
            catch (Exception problem) when (!cancellationToken.IsCancellationRequested)
            {
                failure = Describe(problem);
            }
            if (attempt < attempts)
            {
                await Task.Delay(RetryAfter, _time, cancellationToken);
            }
        }
        return failure;
    }

    // Only the type name, and for the sinks' own AnalyticsSendException its message (they build it without the site key). Never the
    // exception object, its text or its inner exceptions: an HttpRequestException can carry the request URL.
    private static string Describe(Exception problem)
        => problem is AnalyticsSendException ? $"{nameof(AnalyticsSendException)}: {problem.Message}" : problem.GetType().Name;

    // The target is read when events arrive, and again once it is 30 seconds old. A read that fails keeps the last target.
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
        Warn(provider, $"Dropped {count} usage events: {reason}");
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
        _logger.LogWarning("Analytics delivery to {Provider}: {Message}", provider, message);
    }

    private static string ProviderTag(AnalyticsProvider provider) => provider switch
    {
        AnalyticsProvider.Umami => "umami",
        AnalyticsProvider.PostHog => "posthog",
        _ => "none",
    };
}
