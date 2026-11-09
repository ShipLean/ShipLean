namespace ShipLean.Application.Features.Backups.Runs;

// What a new run row records. ScheduledFor is the cron occurrence (Scheduled only, null for Manual); UserId is the admin
// behind a Manual run. PassphraseGeneration and DestinationId come from the run's config snapshot.
public sealed record BackupStart(BackupTriggerKind Trigger, Guid? UserId, DateTimeOffset? ScheduledFor, int PassphraseGeneration, string DestinationId);
