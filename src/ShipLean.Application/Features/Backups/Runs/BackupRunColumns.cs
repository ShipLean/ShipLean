namespace ShipLean.Application.Features.Backups.Runs;

// backup_run read as BackupRunColumns.Row (Dapper maps by name, so every column is aliased). Constant, so statements
// that use it are constants too: a statement that joins must not bring in another column of these names.
internal static class BackupRunColumns
{
    public const string Select =
        """
        id AS Id, trigger AS Trigger, state AS State, scheduled_for AS ScheduledFor, started_at AS StartedAt, finished_at AS FinishedAt,
        started_by AS StartedBy, passphrase_generation AS PassphraseGeneration, database_object_key AS DatabaseObjectKey,
        database_bytes AS DatabaseBytes, attachments_copied AS AttachmentsCopied, attachments_bytes AS AttachmentsBytes, error AS Error,
        pruned_at AS PrunedAt, host AS Host, destination_id AS DestinationId
        """;

    internal sealed record Row(
        Guid Id,
        string Trigger,
        string State,
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
        string? DestinationId)
    {
        public BackupRunRow ToRun() => new(
            Id, BackupRunWire.Trigger(Trigger), BackupRunWire.State(State), ScheduledFor, StartedAt, FinishedAt, StartedBy, PassphraseGeneration,
            DatabaseObjectKey, DatabaseBytes, AttachmentsCopied, AttachmentsBytes, Error, PrunedAt, Host, DestinationId);
    }
}
