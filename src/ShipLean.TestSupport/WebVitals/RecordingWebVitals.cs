using ShipLean.Contracts.Analytics;

namespace ShipLean.TestSupport.WebVitals;

// The test host's IWebVitalsQueue: keeps what was queued so a test can assert on it. WaitForAsync completes when a matching sample
// arrives (or already has), by signal and not by polling.
public sealed class RecordingWebVitals : IWebVitalsQueue
{
    private readonly object _gate = new();
    private readonly List<WebVitalsSample> _samples = [];
    private readonly List<(Func<WebVitalsSample, bool> Match, TaskCompletionSource<WebVitalsSample> Source)> _waiters = [];

    public IReadOnlyList<WebVitalsSample> Samples
    {
        get
        {
            lock (_gate)
            {
                return [.. _samples];
            }
        }
    }

    public void Track(WebVitalsSample sample)
    {
        List<TaskCompletionSource<WebVitalsSample>> done = [];
        lock (_gate)
        {
            _samples.Add(sample);
            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                if (_waiters[i].Match(sample))
                {
                    done.Add(_waiters[i].Source);
                    _waiters.RemoveAt(i);
                }
            }
        }
        foreach (var source in done)
        {
            source.TrySetResult(sample);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _samples.Clear();
        }
    }

    public async Task<WebVitalsSample> WaitForAsync(Func<WebVitalsSample, bool> match, TimeSpan timeout)
    {
        TaskCompletionSource<WebVitalsSample> source = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            var existing = _samples.Find(e => match(e));
            if (existing is not null)
            {
                return existing;
            }
            _waiters.Add((match, source));
        }
        using var timer = new CancellationTokenSource(timeout);
        await using var registration = timer.Token.Register(() => source.TrySetException(new TimeoutException($"No matching web vitals sample within {timeout}.")));
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
