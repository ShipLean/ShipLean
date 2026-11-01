using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Usage;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.Usage.Delivery;

// The real usage pipeline (queue, dispatcher, mapper, project lookup on the clone) against a recording sink, a settings double and a
// fake clock. Nothing sleeps: a test waits on the sink's or the reader's signal, and moves the clock itself.
[TestFixture]
public class UsageDispatcherTests : ApiFixture
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly FixedAnalyticsSettingsReader _reader = new();
    private readonly ProbeSink _umami = new(AnalyticsProvider.Umami);
    private readonly ProbeSink _postHog = new(AnalyticsProvider.PostHog);

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        UsageServices.Register(services);
        services.AddSingleton<TimeProvider>(_clock);
        services.AddSingleton<IAnalyticsSettingsReader>(_reader);
        services.AddSingleton<IAnalyticsSink>(_umami);
        services.AddSingleton<IAnalyticsSink>(_postHog);
    };

    private IUsageEvents Usage => Host.Services.GetRequiredService<IUsageEvents>();

    private static AnalyticsTarget Target(AnalyticsProvider provider)
        => new(provider, new Uri("https://analytics.example.com"), "site-key-0001", "ship.example.com", false, "1.0.0");

    // The dispatcher keeps a target for 30 seconds, so a test that changes it moves the clock past that first.
    private void UseTarget(AnalyticsTarget? target)
    {
        _reader.Target = target;
        _clock.Advance(UsageDispatcher.SettingsLifetime + TimeSpan.FromSeconds(1));
    }

    private void TrackMany(int count, string source, Guid? projectId = null)
    {
        for (var i = 0; i < count; i++)
        {
            Usage.Track(new UsageEvent(
                UsageEventNames.ProjectSettingsChanged, UsageSurface.Web, UsageActor.ForUser(SeedIds.Users.Joel), projectId, projectId is null ? 0 : 1,
                UsageOutcome.Ok, null, 5, source, _clock.GetUtcNow(), new Dictionary<string, object>()));
        }
    }

    // Moves the fake clock a second at a time until the task is done: the dispatcher's flush and retry timers are on it, and a test
    // cannot tell when each one has been set.
    private async Task AdvanceUntilAsync(Task done)
    {
        using var limit = new CancellationTokenSource(Limit);
        while (!done.IsCompleted)
        {
            limit.Token.ThrowIfCancellationRequested();
            _clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }
        await done;
    }

    private static async Task WaitAsync(SemaphoreSlim signal)
    {
        using var limit = new CancellationTokenSource(Limit);
        await signal.WaitAsync(limit.Token);
    }

    private static MeterListener ListenToFailed(TaskCompletionSource<(long Value, string? Provider)> failed)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == UsageQueue.MeterName && instrument.Name == UsageDispatcher.FailedCounterName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string? provider = null;
            foreach (var tag in tags)
            {
                if (tag.Key == "provider")
                {
                    provider = tag.Value as string;
                }
            }
            failed.TrySetResult((value, provider));
        });
        listener.Start();
        return listener;
    }

    [Test]
    [Order(1)]
    public async Task Dispatcher_WhenEventsTracked_ShouldDeliverOneBatchToConfiguredSink()
    {
        UseTarget(Target(AnalyticsProvider.Umami));

        TrackMany(UsageDispatcher.MaxBatchSize, "test:one", SeedIds.Projects.Ship);
        await WaitAsync(_umami.Attempts);

        var call = _umami.Inner.Calls.Single();
        call.Batch.Count.ShouldBe(UsageDispatcher.MaxBatchSize);
        call.Target.Provider.ShouldBe(AnalyticsProvider.Umami);
        call.Batch.ShouldAllBe(e => e.ProjectKey == "SHIP" && e.Name == UsageEventNames.ProjectSettingsChanged);
        _postHog.AttemptCount.ShouldBe(0);
    }

    [Test]
    [Order(2)]
    public async Task Dispatcher_When100Events_ShouldFlushWithoutWaiting()
    {
        UseTarget(Target(AnalyticsProvider.Umami));
        var before = _umami.Inner.Calls.Count;
        var time = _clock.GetUtcNow();

        TrackMany(UsageDispatcher.MaxBatchSize, "test:full");
        await WaitAsync(_umami.Attempts);

        _umami.Inner.Calls.Count.ShouldBe(before + 1);
        _umami.Inner.Calls[^1].Batch.Count.ShouldBe(UsageDispatcher.MaxBatchSize);
        _clock.GetUtcNow().ShouldBe(time);
    }

    [Test]
    [Order(3)]
    public async Task Dispatcher_WhenTwoSecondsPass_ShouldFlushPartialBatch()
    {
        UseTarget(Target(AnalyticsProvider.Umami));
        var before = _umami.Inner.Calls.Count;

        TrackMany(3, "test:partial");
        await AdvanceUntilAsync(_umami.Attempts.WaitAsync(Limit));

        var batch = _umami.Inner.Calls[before].Batch;
        batch.Count.ShouldBe(3);
        batch.ShouldAllBe(e => e.Properties[AnalyticsProps.Source].Equals("test:partial"));
    }

    [Test]
    [Order(4)]
    public async Task Dispatcher_WhenProviderNone_ShouldDropAndSendNothing()
    {
        var reads = _reader.ReadCount;
        UseTarget(null);
        var attempts = _umami.AttemptCount;

        TrackMany(UsageDispatcher.MaxBatchSize, "test:none");
        await _reader.WaitForReadsAsync(reads + 1);
        UseTarget(Target(AnalyticsProvider.Umami));
        var before = _umami.Inner.Calls.Count;
        TrackMany(UsageDispatcher.MaxBatchSize, "test:after");
        await WaitAsync(_umami.Attempts);

        // The batch tracked while the provider was none went nowhere; the next one went to the sink.
        _umami.Inner.Calls.Count.ShouldBe(before + 1);
        _umami.Inner.Calls[^1].Batch.ShouldAllBe(e => e.Properties[AnalyticsProps.Source].Equals("test:after"));
        _umami.AttemptCount.ShouldBe(attempts + 1);
        _postHog.AttemptCount.ShouldBe(0);
    }

    [Test]
    [Order(5)]
    public async Task Dispatcher_WhenSinkThrowsTwice_ShouldDropBatchAndCountFailure()
    {
        UseTarget(Target(AnalyticsProvider.Umami));
        _umami.Inner.FailWith("provider down");
        var attempts = _umami.AttemptCount;
        TaskCompletionSource<(long Value, string? Provider)> failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = ListenToFailed(failed);
        try
        {
            TrackMany(UsageDispatcher.MaxBatchSize, "test:failing");
            await AdvanceUntilAsync(failed.Task);
        }
        finally
        {
            _umami.Inner.FailWith(null);
        }

        var (value, provider) = await failed.Task;
        value.ShouldBe(UsageDispatcher.MaxBatchSize);
        provider.ShouldBe("umami");
        _umami.AttemptCount.ShouldBe(attempts + 2);
        _umami.Inner.Events.ShouldNotContain(e => e.Properties[AnalyticsProps.Source].Equals("test:failing"));
    }

    [Test]
    [Order(6)]
    public async Task Dispatcher_WhenSinkHangs_ShouldNotBlockTrack()
    {
        UseTarget(Target(AnalyticsProvider.PostHog));
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _postHog.Gate = gate;
        try
        {
            TrackMany(UsageDispatcher.MaxBatchSize, "test:stuck");
            await WaitAsync(_postHog.Started);

            // The sink is now stuck inside SendAsync.
            Should.CompleteIn(() => TrackMany(50, "test:behind"), TimeSpan.FromSeconds(5));
        }
        finally
        {
            gate.SetResult();
        }
        await AdvanceUntilAsync(_postHog.Attempts.WaitAsync(Limit));
        await AdvanceUntilAsync(_postHog.Attempts.WaitAsync(Limit));

        _postHog.Inner.Calls[0].Batch.Count.ShouldBe(UsageDispatcher.MaxBatchSize);
    }

    // A dispatcher of its own (own queue and settings), on the shared fake clock, so stopping it leaves the host's one alone.
    private UsageDispatcher OwnDispatcher(UsageQueue queue, IAnalyticsSink sink, ILogger<UsageDispatcher> logger)
    {
        FixedAnalyticsSettingsReader reader = new() { Target = Target(sink.Provider) };
        return new UsageDispatcher(
            queue, Host.Services.GetRequiredService<UsageEventMapper>(), reader, [sink], _clock,
            Host.Services.GetRequiredService<IMeterFactory>(), logger);
    }

    private void TrackOn(UsageQueue queue, int count)
    {
        for (var i = 0; i < count; i++)
        {
            queue.Track(new UsageEvent(
                UsageEventNames.ProjectSettingsChanged, UsageSurface.Web, UsageActor.ForUser(SeedIds.Users.Joel), null, 0,
                UsageOutcome.Ok, null, 5, "test:own", _clock.GetUtcNow(), new Dictionary<string, object>()));
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Dispatcher_WhenSinkNeverReturnsAndIgnoresToken_ShouldStopAtStopCap(bool sinkAlreadyInside)
    {
        ProbeSink stuck = new(AnalyticsProvider.Umami) { Gate = new TaskCompletionSource() };
        UsageQueue queue = new(Host.Services.GetRequiredService<IMeterFactory>());
        var dispatcher = OwnDispatcher(queue, stuck, new CaptureLogger());
        await dispatcher.StartAsync(CancellationToken.None);
        TrackOn(queue, 3);
        if (sinkAlreadyInside)
        {
            await AdvanceUntilAsync(stuck.Started.WaitAsync(Limit));
        }

        var started = _clock.GetUtcNow();
        await AdvanceUntilAsync(dispatcher.StopAsync(CancellationToken.None));

        (_clock.GetUtcNow() - started).ShouldBeLessThan(UsageDispatcher.StopCap + TimeSpan.FromSeconds(3));
        dispatcher.Dispose();
    }

    [Test]
    public async Task Dispatcher_WhenSinkThrowsWithSiteKeyInMessage_ShouldNotLogTheKey()
    {
        const string key = "SECRET-SITE-KEY-9f3a";
        CaptureLogger logger = new();
        ThrowingSink sink = new(AnalyticsProvider.Umami, new HttpRequestException($"POST https://analytics.example.com/?key={key} failed"));
        UsageQueue queue = new(Host.Services.GetRequiredService<IMeterFactory>());
        var dispatcher = OwnDispatcher(queue, sink, logger);
        await dispatcher.StartAsync(CancellationToken.None);

        TrackOn(queue, 3);
        await AdvanceUntilAsync(logger.Warned.Task);
        await AdvanceUntilAsync(dispatcher.StopAsync(CancellationToken.None));
        dispatcher.Dispose();

        logger.Lines.ShouldNotBeEmpty();
        logger.Lines.ShouldAllBe(line => !line.Contains(key));
        logger.Lines.ShouldContain(line => line.Contains(nameof(HttpRequestException)));
    }

    private sealed class ThrowingSink(AnalyticsProvider provider, Exception failure) : IAnalyticsSink
    {
        public AnalyticsProvider Provider => provider;

        public Task SendAsync(IReadOnlyList<AnalyticsEvent> batch, AnalyticsTarget target, CancellationToken cancellationToken)
            => throw failure;
    }

    // Keeps every line as a real provider would write it, exception text included.
    private sealed class CaptureLogger : ILogger<UsageDispatcher>
    {
        private readonly object _gate = new();
        private readonly List<string> _lines = [];

        public TaskCompletionSource Warned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_gate)
                {
                    return [.. _lines];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_gate)
            {
                _lines.Add($"{formatter(state, exception)} {exception}");
            }
            Warned.TrySetResult();
        }
    }
}
