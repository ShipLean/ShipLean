using ShipLean.Application.Features.Backups.Cron;
using ShipLean.Application.Features.Backups.Runs;
using ShipLean.Contracts.Backups.Dtos;
using BackupRunState = ShipLean.Application.Features.Backups.Runs.BackupRunState;

namespace ShipLean.Application.Features.Backups.Admin;

// The health line (D9), first match wins: notConfigured (nothing saved), running (a run holds the lease), failing (the
// last finished run failed), overdue (enabled, and an occurrence came and went over an hour ago with no success since),
// neverRun (no success yet), ok. A Running row whose lease is free belongs to an instance that stopped: it counts as a
// failed run, as the next start will mark it abandoned.
public static class BackupHealthRule
{
    public static readonly TimeSpan Grace = TimeSpan.FromHours(1);

    // recent: newest first. leaseHeld: some session holds the backup lease in this database.
    // The overdue clock starts at the later of the last save and the last success, so a schedule that was just saved
    // (or changed) is not overdue for occurrences before it.
    public static BackupHealth Compute(
        BackupSettingsRow? settings,
        IReadOnlyList<BackupRunRow> recent,
        bool leaseHeld,
        BackupRunRow? lastSuccess,
        ICronSchedule schedule,
        DateTimeOffset now)
    {
        if (settings is null)
        {
            return BackupHealth.NotConfigured;
        }
        var newest = recent.Count > 0 ? recent[0] : null;
        if (newest is { State: BackupRunState.Running } && leaseHeld)
        {
            return BackupHealth.Running;
        }
        var lastFinished = recent.FirstOrDefault(r => r.State != BackupRunState.Running || !leaseHeld);
        if (lastFinished is { State: BackupRunState.Failed or BackupRunState.Running })
        {
            return BackupHealth.Failing;
        }
        if (settings.Enabled)
        {
            var since = lastSuccess is null || lastSuccess.StartedAt < settings.UpdatedAt ? settings.UpdatedAt : lastSuccess.StartedAt;
            if (schedule.Next(settings.Cron, since) is { } due && due + Grace <= now)
            {
                return BackupHealth.Overdue;
            }
        }
        return lastSuccess is null ? BackupHealth.NeverRun : BackupHealth.Ok;
    }
}
