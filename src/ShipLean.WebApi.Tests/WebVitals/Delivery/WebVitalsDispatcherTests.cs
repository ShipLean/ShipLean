using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.WebVitals;
using ShipLean.Contracts.Analytics;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Analytics;
using ShipLean.TestSupport.Usage;
using ShipLean.WebApi.Tests.Backups.Job;

namespace ShipLean.WebApi.Tests.WebVitals.Delivery;

// The real queue and dispatcher against a recording sink, a settings double and a fake clock. Nothing sleeps: a test waits on the
// sink's signal and moves the clock itself.
[TestFixture]
public class WebVitalsDispatcherTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private readonly FakeTimeProvider _clock = new(TestDates.Today.AddHours(9));
    private FixedAnalyticsSettingsReader _reader = null!;
    private ScopedMeterFactory _meters = null!;
    private WebVitalsQueue _queue = null!;
    private ProbeSink _umami = null!;
    private WebVitalsDispatcher _dispatcher = null!;

    [SetUp]
    public async Task StartDispatcher()
    {
        _reader = new FixedAnalyticsSettingsReader { Target = Target(AnalyticsProvider.Umami, true) };
        _meters = new ScopedMeterFactory();
        _queue = new WebVitalsQueue(_meters);
        _umami = new ProbeSink(AnalyticsProvider.Umami);
        _dispatcher = new WebVitalsDispatcher(
            _queue, _reader, [_umami, new ProbeSink(AnalyticsProvider.PostHog)], _clock, _meters, NullLogger<WebVitalsDispatcher>.Instance);
        await _dispatcher.StartAsync(CancellationToken.None);
    }

    [TearDown]
    public async Task StopDispatcher()
    {
        _umami.Gate?.TrySetResult();
        await _dispatcher.StopAsync(CancellationToken.None);
        _dispatcher.Dispose();
        _meters.Dispose();
    }

    private static AnalyticsTarget Target(AnalyticsProvider provider, bool sendWebVitals)
        => new(provider, new Uri("https://analytics.example.com"), "site-key-0001", "ship.example.com", false, "1.0.0", sendWebVitals);

    private void Track(int count, string screen)
    {
        for (var i = 0; i < count; i++)
        {
            _queue.Track(new WebVitalsSample(
                screen, WebVitalsNavigation.Soft, 800, 90, 0.02, null, null, "Mozilla/5.0", "1920x1080", "user:1", _clock.GetUtcNow()));
        }
    }

    // The dispatcher keeps a target for 30 seconds, so a test that changes it moves the clock past that first.
    private void UseTarget(AnalyticsTarget? target)
    {
        _reader.Target = target;
        _clock.Advance(WebVitalsDispatcher.SettingsLifetime + TimeSpan.FromSeconds(1));
    }

    // Moves the fake clock a second at a time until the task is done: the flush timer is on it and a test cannot tell when it is set.
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

    // Fails the test when the task is not done in real time.
    private static async Task<T> WithinAsync<T>(Task<T> task, TimeSpan limit)
    {
        await WithinAsync((Task)task, limit);
        return await task;
    }

    private static async Task WithinAsync(Task task, TimeSpan limit)
    {
        using var timer = new CancellationTokenSource();
        if (await Task.WhenAny(task, Task.Delay(limit, timer.Token)) != task)
        {
            throw new TimeoutException($"Not done within {limit.TotalSeconds:0} s.");
        }
        await timer.CancelAsync();
        await task;
    }

    private MeterListener Listen(string counter, TaskCompletionSource<(long Value, string? Provider)> measured)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Scope == _meters && instrument.Name == counter)
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
            measured.TrySetResult((value, provider));
        });
        listener.Start();
        return listener;
    }

    [Test]
    public async Task Dispatcher_WhenSwitchOn_ShouldSendToTheConfiguredSink()
    {
        TaskCompletionSource<(long Value, string? Provider)> sent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = Listen(WebVitalsDispatcher.SentCounterName, sent);

        Track(3, "taskboard");
        await AdvanceUntilAsync(WaitAsync(_umami.Attempts));

        var call = _umami.Inner.Calls.Single();
        call.Batch.Count.ShouldBe(3);
        call.Batch.ShouldAllBe(s => s.Screen == "taskboard");
        call.Target.Provider.ShouldBe(AnalyticsProvider.Umami);
        var (value, provider) = await WithinAsync(sent.Task, Limit);
        value.ShouldBe(3);
        provider.ShouldBe("umami");
    }

    [Test]
    public async Task Dispatcher_WhenSwitchOff_ShouldSendNothing()
    {
        UseTarget(Target(AnalyticsProvider.Umami, false));
        var reads = _reader.ReadCount;
        Track(100, "off");
        await WithinAsync(_reader.WaitForReadsAsync(reads + 1), Limit);

        // The same path with the switch on delivers, and only the later batch arrives.
        UseTarget(Target(AnalyticsProvider.Umami, true));
        Track(100, "on");
        await WaitAsync(_umami.Attempts);

        _umami.AttemptCount.ShouldBe(1);
        _umami.Inner.Samples.ShouldAllBe(s => s.Screen == "on");
        _umami.Inner.Samples.Count.ShouldBe(100);
    }

    [Test]
    public async Task Dispatcher_WhenProviderNone_ShouldSendNothing()
    {
        UseTarget(null);
        var reads = _reader.ReadCount;
        Track(100, "none");
        await WithinAsync(_reader.WaitForReadsAsync(reads + 1), Limit);

        UseTarget(Target(AnalyticsProvider.Umami, true));
        Track(100, "after");
        await WaitAsync(_umami.Attempts);

        _umami.AttemptCount.ShouldBe(1);
        _umami.Inner.Samples.ShouldAllBe(s => s.Screen == "after");
    }

    [Test]
    public async Task Dispatcher_WhenSinkFails_ShouldCountAndNotRetry()
    {
        _umami.Inner.FailWith("provider down");
        TaskCompletionSource<(long Value, string? Provider)> failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var listener = Listen(WebVitalsDispatcher.FailedCounterName, failed);

        Track(100, "failing");
        var (value, provider) = await WithinAsync(failed.Task, Limit);

        value.ShouldBe(100);
        provider.ShouldBe("umami");
        await WaitAsync(_umami.Attempts);
        // Time well past any retry delay: still the one call.
        _clock.Advance(TimeSpan.FromMinutes(5));
        _umami.Inner.FailWith(null);
        Track(1, "next");
        await AdvanceUntilAsync(WaitAsync(_umami.Attempts));
        _umami.AttemptCount.ShouldBe(2);
        _umami.Inner.Samples.ShouldAllBe(s => s.Screen == "next");
    }

    [Test]
    public async Task Dispatcher_WhenFullBatch_ShouldFlushBeforeTheTimer()
    {
        var time = _clock.GetUtcNow();

        Track(WebVitalsDispatcher.MaxBatchSize, "full");
        await WaitAsync(_umami.Attempts);

        _umami.Inner.Calls.Single().Batch.Count.ShouldBe(WebVitalsDispatcher.MaxBatchSize);
        _clock.GetUtcNow().ShouldBe(time);
    }

    [Test]
    public async Task Dispatcher_WhenPartialBatch_ShouldFlushAfterFiveSeconds()
    {
        Track(2, "partial");
        var started = _clock.GetUtcNow();
        await AdvanceUntilAsync(WaitAsync(_umami.Attempts));

        _umami.Inner.Calls.Single().Batch.Count.ShouldBe(2);
        (_clock.GetUtcNow() - started).ShouldBeGreaterThanOrEqualTo(WebVitalsDispatcher.FlushAfter);
    }

    [Test]
    public async Task Dispatcher_WhenStopped_ShouldReturnPromptly()
    {
        TaskCompletionSource gate = new();
        _umami.Gate = gate;
        Track(100, "stuck");
        await WaitAsync(_umami.Started);
        Track(5, "queued");

        // The sink is stuck inside SendAsync and ignores its token; the clock does not move.
        await WithinAsync(_dispatcher.StopAsync(CancellationToken.None), TimeSpan.FromSeconds(5));

        gate.SetResult();
        _umami.Inner.Samples.ShouldNotContain(s => s.Screen == "queued");
    }

    // Wraps a recording sink: signals when a send starts and when it is attempted, and can hold a send open.
    private sealed class ProbeSink(AnalyticsProvider provider) : IWebVitalsSink
    {
        private int _attempts;

        public RecordingWebVitalsSink Inner { get; } = new(provider);

        public SemaphoreSlim Started { get; } = new(0);

        public SemaphoreSlim Attempts { get; } = new(0);

        public TaskCompletionSource? Gate { get; set; }

        public int AttemptCount => Volatile.Read(ref _attempts);

        public AnalyticsProvider Provider => provider;

        public async Task SendAsync(IReadOnlyList<WebVitalsSample> batch, AnalyticsTarget target, CancellationToken cancellationToken)
        {
            Started.Release();
            if (Gate is { } gate)
            {
                await gate.Task;
            }
            try
            {
                await Inner.SendAsync(batch, target, cancellationToken);
            }
            finally
            {
                Interlocked.Increment(ref _attempts);
                Attempts.Release();
            }
        }
    }
}
