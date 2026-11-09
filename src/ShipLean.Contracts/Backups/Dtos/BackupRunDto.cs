using System.Text.Json.Serialization;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Backups.Dtos;

// One run in the history, and the 202 body of POST /api/instance/backups/run. StartedBy is null for a scheduled run.
// The object and size fields are null until the run succeeds. DurationLabel is null while running and for an abandoned
// run, whose FinishedAt is when another instance marked it.
public sealed record BackupRunDto(
    Guid Id,
    BackupRunTrigger Trigger,
    BackupRunState State,
    [property: JsonConverter(typeof(UtcSecondsJsonConverter))] DateTimeOffset StartedAt,
    string StartedLabel,
    [property: JsonConverter(typeof(UtcSecondsJsonConverter))] DateTimeOffset? FinishedAt,
    string? DurationLabel,
    UserRef? StartedBy,
    int PassphraseGeneration,
    string? DatabaseObjectKey,
    long? DatabaseBytes,
    int? AttachmentsCopied,
    long? AttachmentsBytes,
    string? Error,
    bool Pruned);
