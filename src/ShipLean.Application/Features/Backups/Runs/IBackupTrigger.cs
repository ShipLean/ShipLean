namespace ShipLean.Application.Features.Backups.Runs;

// Starts a backup run: reads the config once, takes the lease and hands both to IBackupJob in the background. A Manual
// request that is Busy throws ApiException.Conflict("A backup is already running."); no saved config throws
// ApiException.NotConfigured. A Scheduled request returns its refusal instead of throwing.
public interface IBackupTrigger
{
    Task<BackupTriggerResult> RequestAsync(BackupTriggerKind kind, Guid? userId, DateTimeOffset? scheduledFor, CancellationToken cancellationToken = default);
}
