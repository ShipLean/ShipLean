using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Backups.Schedule;

// Starts a run (contract C-RUN): reads the config once, takes the lease with that snapshot's generation and destination,
// and queues both for BackupWorker. The caller gets the run's row as it started (Running) and never waits for the job.
public sealed class BackupTrigger(IBackupConfigReader configs, IBackupRunLog runLog, BackupQueue queue) : IBackupTrigger
{
    public const string BusyMessage = "A backup is already running.";
    public const string NotConfiguredMessage = "Backups are not set up. Save the backup settings first.";
    public const string WorkerStoppedError = "The backup worker had stopped, so the run never began.";

    public async Task<BackupTriggerResult> RequestAsync(BackupTriggerKind kind, Guid? userId, DateTimeOffset? scheduledFor, CancellationToken cancellationToken = default)
    {
        var snapshot = await configs.ReadAsync(cancellationToken) ?? throw ApiException.NotConfigured(NotConfiguredMessage);
        var started = await runLog.TryStartAsync(new BackupStart(kind, userId, scheduledFor, snapshot.PassphraseGeneration, snapshot.DestinationId), cancellationToken);
        if (started.Lease is not { } lease)
        {
            if (kind == BackupTriggerKind.Manual && started.Refusal == BackupRefusal.Busy)
            {
                throw ApiException.Conflict(BusyMessage);
            }
            return new BackupTriggerResult(null, started.Refusal);
        }
        if (!queue.TryEnqueue(new BackupRunRequest(lease, snapshot)))
        {
            // The host is stopping: nothing will run it, so close the row now rather than leave it to read as abandoned.
            await using (lease)
            {
                await runLog.FailAsync(lease.Run.Id, WorkerStoppedError, CancellationToken.None);
            }
            throw new InvalidOperationException(WorkerStoppedError);
        }
        return new BackupTriggerResult(lease.Run, null);
    }
}
