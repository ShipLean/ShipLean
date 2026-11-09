using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Runs;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// IBackupJob for the worker's scope: reports its start, waits at the probe's gate, then records success (or throws).
// The worker disposes its scope after the lease, so DisposeAsync reports a released lease.
public sealed class ProbeJob(JobProbe probe, IBackupRunLog runLog) : IBackupJob, IDisposable, IAsyncDisposable
{
    private Guid _runId;

    public async Task RunAsync(BackupLease lease, BackupConfig snapshot, CancellationToken cancellationToken = default)
    {
        _runId = lease.Run.Id;
        probe.Started(lease.Run, snapshot);
        if (probe.Gate is { } gate)
        {
            await using (cancellationToken.Register(() => gate.TrySetCanceled(cancellationToken)))
            {
                await gate.Task;
            }
        }
        if (probe.Throw)
        {
            throw new InvalidOperationException("The probe job failed on purpose.");
        }
        await runLog.SucceedAsync(lease.Run.Id, new BackupOutcome($"database/probe-{lease.Run.Id:N}.dump.slbk", 1, 0, 0), cancellationToken);
    }

    public void Dispose()
    {
        if (_runId != Guid.Empty)
        {
            probe.Ended(_runId);
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
