namespace ShipLean.Application.Features.Backups.Runs;

// Who asked for a run: the cron schedule or an instance admin's Run now. Stored as "scheduled" / "manual".
public enum BackupTriggerKind
{
    Scheduled,
    Manual,
}
