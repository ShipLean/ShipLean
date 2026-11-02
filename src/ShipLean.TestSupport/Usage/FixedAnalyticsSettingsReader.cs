using ShipLean.Contracts.Analytics;

namespace ShipLean.TestSupport.Usage;

// An IAnalyticsSettingsReader that answers with the target a test sets (null: provider none) and counts its reads.
// WaitForReadsAsync completes when the count reaches a number, by signal and not by polling.
public sealed class FixedAnalyticsSettingsReader : IAnalyticsSettingsReader
{
    private readonly object _gate = new();
    private readonly List<(int AtLeast, TaskCompletionSource Source)> _waiters = [];
    private AnalyticsTarget? _target;
    private int _reads;

    public AnalyticsTarget? Target
    {
        get
        {
            lock (_gate)
            {
                return _target;
            }
        }
        set
        {
            lock (_gate)
            {
                _target = value;
            }
        }
    }

    public int ReadCount
    {
        get
        {
            lock (_gate)
            {
                return _reads;
            }
        }
    }

    public Task<AnalyticsTarget?> GetTargetAsync(CancellationToken cancellationToken)
    {
        List<TaskCompletionSource> done = [];
        AnalyticsTarget? target;
        lock (_gate)
        {
            _reads++;
            target = _target;
            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                if (_reads >= _waiters[i].AtLeast)
                {
                    done.Add(_waiters[i].Source);
                    _waiters.RemoveAt(i);
                }
            }
        }
        foreach (var source in done)
        {
            source.TrySetResult();
        }
        return Task.FromResult(target);
    }

    public Task WaitForReadsAsync(int atLeast)
    {
        lock (_gate)
        {
            if (_reads >= atLeast)
            {
                return Task.CompletedTask;
            }
            TaskCompletionSource source = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add((atLeast, source));
            return source.Task;
        }
    }
}
