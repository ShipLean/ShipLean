using ShipLean.Contracts.Usage;

namespace ShipLean.TestSupport.Usage;

// The test host's IUsageEvents: keeps what was tracked so a test can assert on it. WaitForAsync completes when a matching event
// arrives (or already has), by signal and not by polling.
public sealed class RecordingUsageEvents : IUsageEvents
{
    private readonly object _gate = new();
    private readonly List<UsageEvent> _events = [];
    private readonly List<(Func<UsageEvent, bool> Match, TaskCompletionSource<UsageEvent> Source)> _waiters = [];

    public IReadOnlyList<UsageEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public void Track(UsageEvent usageEvent)
    {
        List<TaskCompletionSource<UsageEvent>> done = [];
        lock (_gate)
        {
            _events.Add(usageEvent);
            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                if (_waiters[i].Match(usageEvent))
                {
                    done.Add(_waiters[i].Source);
                    _waiters.RemoveAt(i);
                }
            }
        }
        foreach (var source in done)
        {
            source.TrySetResult(usageEvent);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _events.Clear();
        }
    }

    public async Task<UsageEvent> WaitForAsync(Func<UsageEvent, bool> match, TimeSpan timeout)
    {
        TaskCompletionSource<UsageEvent> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            var existing = _events.Find(e => match(e));
            if (existing is not null)
            {
                return existing;
            }
            _waiters.Add((match, source));
        }
        using var timer = new CancellationTokenSource(timeout);
        await using var registration = timer.Token.Register(() => source.TrySetException(new TimeoutException($"No matching usage event within {timeout}.")));
        try
        {
            return await source.Task;
        }
        catch (TimeoutException)
        {
            lock (_gate)
            {
                _waiters.RemoveAll(w => w.Source == source);
            }
            throw;
        }
    }
}
