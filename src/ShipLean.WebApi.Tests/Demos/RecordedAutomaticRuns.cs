namespace ShipLean.WebApi.Tests.Demos;

// Counts the automatic runs that have finished, per sprint, and lets a test wait for the Nth one. Delivery of SprintClosed is asynchronous,
// so a test waits on this instead of reading the database right after its request.
internal sealed class RecordedAutomaticRuns
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, int> _finished = [];
    private readonly List<(Guid SprintId, int Count, TaskCompletionSource Done)> _waiters = [];

    // While set, the run throws instead of running (the double for a run that fails).
    public volatile bool Fail;

    public void Finished(Guid sprintId)
    {
        lock (_gate)
        {
            _finished[sprintId] = _finished.GetValueOrDefault(sprintId) + 1;
            foreach (var waiter in _waiters.Where(w => w.SprintId == sprintId && _finished[sprintId] >= w.Count))
            {
                waiter.Done.TrySetResult();
            }
        }
    }

    public async Task WaitAsync(Guid sprintId, int count = 1)
    {
        TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            if (_finished.GetValueOrDefault(sprintId) >= count)
            {
                return;
            }

            _waiters.Add((sprintId, count, done));
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var _ = timeout.Token.Register(() => done.TrySetCanceled(timeout.Token));
        await done.Task;
    }
}
