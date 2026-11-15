namespace ShipLean.WebApi.Tests.Backups;

// Bounds a wait in the backup tests at 30 s. AG0032 bans Task.Wait*/WaitAsync, so the bound is a
// CancellationTokenSource whose token completes a source the wait races against.
internal static class Bounded
{
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    // Completes when task does; throws TimeoutException naming what was awaited if it is not done within 30 s.
    public static async Task WithinAsync(Task task, string what)
    {
        using var limit = new CancellationTokenSource(Limit);
        var timedOut = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = limit.Token.Register(() => timedOut.TrySetResult());
        if (await Task.WhenAny(task, timedOut.Task) != task)
        {
            throw new TimeoutException($"Timed out after {Limit.TotalSeconds:0} s waiting for {what}.");
        }
        await task;
    }
}
