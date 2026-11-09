using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts.Errors;

namespace ShipLean.TestSupport.Backups;

// IBackupTrigger with no config and no job: it takes the lease, records the run as succeeded at once, and releases the
// lease, so a test that runs a backup over HTTP sees it finished when the 202 comes back. A Manual request that is Busy
// throws Conflict, as the real trigger does; it never throws NotConfigured.
public sealed class FakeBackupTrigger(IBackupRunLog runLog) : IBackupTrigger
{
    public const string DestinationId = "fake";

    public async Task<BackupTriggerResult> RequestAsync(BackupTriggerKind kind, Guid? userId, DateTimeOffset? scheduledFor, CancellationToken cancellationToken = default)
    {
        var started = await runLog.TryStartAsync(new BackupStart(kind, userId, scheduledFor, 0, DestinationId), cancellationToken);
        if (started.Lease is not { } lease)
        {
            if (kind == BackupTriggerKind.Manual && started.Refusal == BackupRefusal.Busy)
            {
                throw ApiException.Conflict("A backup is already running.");
            }
            return new BackupTriggerResult(null, started.Refusal);
        }
        await using (lease)
        {
            await runLog.SucceedAsync(lease.Run.Id, new BackupOutcome($"database/fake-{lease.Run.Id:N}.dump.slbk", 0, 0, 0), cancellationToken);
            return new BackupTriggerResult(lease.Run, null);
        }
    }
}
