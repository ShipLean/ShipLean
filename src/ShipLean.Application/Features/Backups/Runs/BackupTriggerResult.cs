namespace ShipLean.Application.Features.Backups.Runs;

// The run that was started (its row as it started, state Running), or why none was.
public sealed record BackupTriggerResult(BackupRunRow? Run, BackupRefusal? Refusal);
