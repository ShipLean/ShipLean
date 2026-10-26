namespace ShipLean.WebApi.Tests.Mcp.Planning.Capture;

// Holds tree writes at their start so a test decides which writes overlap. Armed for a number of callers, the first ones to
// enter wait until that many have entered, then all go on together; unarmed, nothing waits. A wait that never ends fails the
// test after a timeout rather than hanging it.
public sealed class TreeGate
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly object _lock = new();
    private int _callers;
    private int _entered;
    private TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private TaskCompletionSource _enteredTwo = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _waitFor;

    public void Arm(int callers)
    {
        lock (_lock)
        {
            _callers = callers;
            _entered = 0;
            _waitFor = 0;
            _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _enteredTwo = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    // Completes once the given number of writes have entered.
    public Task EnteredAsync(int count)
    {
        lock (_lock)
        {
            _waitFor = count;
            if (_entered >= count)
            {
                _enteredTwo.TrySetResult();
            }
            return WithinAsync(_enteredTwo.Task);
        }
    }

    public async Task PassAsync()
    {
        Task release;
        lock (_lock)
        {
            if (_callers == 0)
            {
                return;
            }

            _entered++;
            if (_waitFor > 0 && _entered >= _waitFor)
            {
                _enteredTwo.TrySetResult();
            }
            if (_entered >= _callers)
            {
                _release.TrySetResult();
            }
            release = _release.Task;
        }

        await WithinAsync(release);
    }

    // Waits for the task, failing the test when it does not finish in time instead of hanging it.
    private static async Task WithinAsync(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(Timeout)) != task)
        {
            throw new TimeoutException("The tree gate was never released: the writes did not overlap.");
        }

        await task;
    }
}
