namespace ShipLean.Contracts.Backups.Dtos;

// What a cron expression means: POST /api/instance/backups/cron-preview and the settings' schedule. An expression that
// doesn't parse has no description and no runs; one that parses but runs too often keeps both, to show why.
public sealed record CronPreviewDto(bool Valid, string? Error, string? Description, IReadOnlyList<CronRunDto> NextRuns);
