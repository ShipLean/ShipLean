namespace ShipLean.Contracts.Backups.Dtos;

// A run as the history shows it. Wire values running, succeeded, failed. Abandoned is failed with its own error, and a
// skipped occurrence is never returned as a run.
public enum BackupRunState
{
    Running,
    Succeeded,
    Failed,
}
