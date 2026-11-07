using System.Threading.Channels;
using Microsoft.Extensions.Time.Testing;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// A FakeTimeProvider seen through one service: each one-shot timer it creates (a Task.Delay, the schedule's sleep) is
// reported with its length, so a test knows the service has finished a tick and is asleep, and for how long. Periodic
// timers (a lease's 15-second check) are not reported. Several clocks can share one FakeTimeProvider.
public sealed class WatchedClock(FakeTimeProvider time) : TimeProvider
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private readonly Channel<TimeSpan> _sleeps = Channel.CreateUnbounded<TimeSpan>();

    public override DateTimeOffset GetUtcNow() => time.GetUtcNow();

    public override long GetTimestamp() => time.GetTimestamp();

    public override long TimestampFrequency => time.TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => time.LocalTimeZone;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = time.CreateTimer(callback, state, dueTime, period);
        if (period == Timeout.InfiniteTimeSpan && dueTime != Timeout.InfiniteTimeSpan)
        {
            _sleeps.Writer.TryWrite(dueTime);
        }
        return timer;
    }

    // The next sleep the service starts (they are consumed in order), or a TimeoutException after 30 s.
    public async Task<TimeSpan> NextSleepAsync()
    {
        using var limit = new CancellationTokenSource(Limit);
        try
        {
            return await _sleeps.Reader.ReadAsync(limit.Token);
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested)
        {
            throw new TimeoutException($"No sleep started within {Limit.TotalSeconds:0} s.");
        }
    }
}
