namespace ShipLean.Application.Features.Backups.Runs;

// The lease's connection, and with it the advisory lock, is gone: another instance may already be running a backup, so
// the holder must stop writing. Thrown by BackupLease.EnsureHeldAsync and IBackupRunLog.ClaimPruneAsync.
public sealed class BackupLeaseLostException(string message, Exception? innerException = null) : Exception(message, innerException)
{
}
