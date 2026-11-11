namespace ShipLean.Application.Features.Backups.Runs;

// One backup_run row. DestinationId is null only on a Skipped row; the outcome columns are set only on Succeeded.
public sealed record BackupRunRow(
    Guid Id,
    BackupTriggerKind Trigger,
    BackupRunState State,
    DateTimeOffset? ScheduledFor,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    Guid? StartedBy,
    int PassphraseGeneration,
    string? DatabaseObjectKey,
    long? DatabaseBytes,
    int? AttachmentsCopied,
    long? AttachmentsBytes,
    string? Error,
    DateTimeOffset? PrunedAt,
    string Host,
    string? DestinationId);
