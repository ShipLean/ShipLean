namespace ShipLean.Application.Features.Backups.Runs;

// A run row's state, stored as "running" / "succeeded" / "failed" / "skipped". An abandoned run is Failed with
// BackupRunLog.AbandonedError. Skipped rows are scheduled occurrences that never started (another run held the lease
// until the next occurrence was due); they only move the schedule's cursor and RecentAsync leaves them out.
public enum BackupRunState
{
    Running,
    Succeeded,
    Failed,
    Skipped,
}
