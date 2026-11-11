using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.Application.Features.Backups.Runs;

// One backup run under a lease the caller took and disposes: dump, upload, mirror, record the outcome, prune. It never
// reads the config itself (snapshot is the run's), runs every side effect under lease.Lost, and never throws for a failed
// backup: it records the failure with IBackupRunLog.FailAsync.
public interface IBackupJob
{
    Task RunAsync(BackupLease lease, BackupConfig snapshot, CancellationToken cancellationToken = default);
}
