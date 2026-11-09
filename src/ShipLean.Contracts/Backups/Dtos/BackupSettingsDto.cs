using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Backups.Dtos;

// GET and PUT /api/instance/backups. Secrets never come back: the destination carries the secret key's last 4, and the
// passphrase only its generation (0 = none set) and when it was set. Runs are newest first, at most 20. Every timestamp
// has a server label, null with it.
public sealed record BackupSettingsDto(
    bool Enabled,
    BackupDestinationDto? Destination,
    string Cron,
    CronPreviewDto Schedule,
    int KeepCount,
    bool IncludeAttachments,
    BackupAttachmentStoreDto AttachmentStore,
    int PassphraseGeneration,
    [property: JsonConverter(typeof(UtcSecondsJsonConverter))] DateTimeOffset? PassphraseSetAt,
    string? PassphraseSetLabel,
    BackupHealth Health,
    [property: JsonConverter(typeof(UtcSecondsJsonConverter))] DateTimeOffset? LastSuccessAt,
    string? LastSuccessLabel,
    IReadOnlyList<BackupRunDto> Runs,
    IReadOnlyList<BackupRegionDto> Regions,
    [property: JsonConverter(typeof(UtcSecondsJsonConverter))] DateTimeOffset? UpdatedAt,
    string? UpdatedLabel);
