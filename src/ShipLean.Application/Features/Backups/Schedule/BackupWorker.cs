using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Features.Backups.Runs;

namespace ShipLean.Application.Features.Backups.Schedule;

// Runs the runs BackupTrigger queued, one at a time: IBackupJob in a scope of its own, under the run's lease, which it
// always disposes. The job records its own outcome; a job that throws anyway has its row failed here. The scope outlives
// the lease, so everything the job resolved is disposed after the lock is released. Test hosts keep the worker, so Back up
// now runs there too (only the schedule is removed).
public sealed class BackupWorker(BackupQueue queue, IServiceScopeFactory scopes, IBackupRunLog runLog, ILogger<BackupWorker> logger) : IHostedService
{
    public const string JobThrewError = "The backup stopped on an unexpected error. The server log has the details.";
    public const string StoppedError = "The instance stopped during the backup.";

    private CancellationTokenSource? _stopping;
    private Task _loop = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stopping = new CancellationTokenSource();
        // Like the holiday job: run without the starter's execution context.
        using (ExecutionContext.SuppressFlow())
        {
            _loop = Task.Run(() => RunAsync(_stopping.Token), CancellationToken.None);
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopping, null) is not { } stopping)
        {
            return;
        }
        queue.Close();
        await stopping.CancelAsync();
        await Task.WhenAny(_loop, Task.Delay(Timeout.Infinite, cancellationToken));
        if (_loop.IsCompleted)
        {
            stopping.Dispose();
        }
        // Started but never picked up: close each row and release its lease.
        while (queue.TryRead(out var left))
        {
            await using (left.Lease)
            {
                await FailQuietlyAsync(left.Lease.Run.Id, StoppedError);
            }
        }
    }

    private async Task RunAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            BackupRunRequest request;
            try
            {
                request = await queue.ReadAsync(stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            await RunOneAsync(request, stopping);
        }
    }

    private async Task RunOneAsync(BackupRunRequest request, CancellationToken stopping)
    {
        var runId = request.Lease.Run.Id;
        await using var scope = scopes.CreateAsyncScope();
        await using (request.Lease)
        {
            try
            {
                await scope.ServiceProvider.GetRequiredService<IBackupJob>().RunAsync(request.Lease, request.Snapshot, stopping);
            }
            catch (OperationCanceledException cancelled) when (stopping.IsCancellationRequested)
            {
                logger.LogWarning(cancelled, "Backup run {RunId} was cut short: the instance is stopping", runId);
                await FailQuietlyAsync(runId, StoppedError);
            }
            catch (Exception failed)
            {
                logger.LogError(failed, "Backup run {RunId} threw; it is recorded as failed", runId);
                await FailQuietlyAsync(runId, JobThrewError);
            }
        }
    }

    // A row the job already finished stays as it is (FailAsync returns false). A database error here is only logged: the
    // next start marks a row still Running as abandoned.
    private async Task FailQuietlyAsync(Guid runId, string error)
    {
        try
        {
            await runLog.FailAsync(runId, error, CancellationToken.None);
        }
        catch (Exception failed)
        {
            logger.LogError(failed, "Could not record backup run {RunId} as failed", runId);
        }
    }
}
