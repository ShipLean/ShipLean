using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Runs;

namespace ShipLean.Application.Fakes.Backups.Runs;

// IBackupJob that writes nothing: it records the run as succeeded with an empty database object.
public sealed class FakeBackupJob(IBackupRunLog runLog) : IBackupJob
{
    public async Task RunAsync(BackupLease lease, BackupConfig snapshot, CancellationToken cancellationToken = default)
        => await runLog.SucceedAsync(lease.Run.Id, new BackupOutcome($"database/fake-{lease.Run.Id:N}.dump.slbk", 0, 0, 0), cancellationToken);
}
