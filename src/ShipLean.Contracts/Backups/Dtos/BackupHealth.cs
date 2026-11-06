namespace ShipLean.Contracts.Backups.Dtos;

// The health line at the top of the Backups tab. Wire values ok, running, failing, overdue, neverRun, notConfigured.
public enum BackupHealth
{
    Ok,
    Running,
    Failing,
    Overdue,
    NeverRun,
    NotConfigured,
}
