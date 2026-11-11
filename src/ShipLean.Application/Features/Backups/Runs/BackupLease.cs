using Npgsql;

namespace ShipLean.Application.Features.Backups.Runs;

// The one-run-at-a-time lease (decision D8): a session advisory lock held on a connection of its own, which is not pooled,
// so the lock can never sit in a pool after the lease is gone. Every 15 seconds the lease checks on that connection that
// it still holds the lock; when the check fails, Lost is cancelled and stays cancelled. Disposing it unlocks and closes
// the connection. A Running row left behind is marked abandoned by the next start.
public sealed class BackupLease : IAsyncDisposable
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

    private readonly CancellationTokenSource _stop = new();
    private readonly PeriodicTimer _timer;
    private readonly Task _checks;
    private int _disposed;

    internal BackupLease(NpgsqlConnection connection, BackupRunRow run, TimeProvider time)
    {
        Connection = new BackupLeaseConnection(connection);
        Run = run;
        _timer = new PeriodicTimer(CheckInterval, time);
        _checks = CheckEveryIntervalAsync();
    }

    // The run's row as it started.
    public BackupRunRow Run { get; }

    // Cancelled once the lease is known to be lost. Run every side effect under it.
    public CancellationToken Lost => Connection.Lost;

    internal BackupLeaseConnection Connection { get; }

    // Checks now, on the lease's connection, that the lock is still held. Throws BackupLeaseLostException when it is not.
    public Task EnsureHeldAsync(CancellationToken cancellationToken = default) => Connection.EnsureHeldAsync(cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }
        await _stop.CancelAsync();
        _timer.Dispose();
        await _checks;
        await Connection.DisposeAsync();
        _stop.Dispose();
    }

    private async Task CheckEveryIntervalAsync()
    {
        try
        {
            while (await _timer.WaitForNextTickAsync(_stop.Token))
            {
                if (!await CheckAsync())
                {
                    await Connection.SignalLostAsync();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            // Disposed.
        }
    }

    // A failed check (any error as much as a missing lock) counts as lost; only disposing stops the checks quietly.
    private async Task<bool> CheckAsync()
    {
        try
        {
            await Connection.EnsureHeldAsync(_stop.Token);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !_stop.IsCancellationRequested)
        {
            return false;
        }
    }
}
