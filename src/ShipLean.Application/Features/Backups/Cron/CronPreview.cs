namespace ShipLean.Application.Features.Backups.Cron;

// A cron expression explained: Valid with no Error, or the reason it is refused. Description (CronExpressionDescriptor,
// 24-hour) and NextRuns (UTC) are filled whenever the expression parses, also when it runs too often; an expression that
// doesn't parse has neither.
public sealed record CronPreview(bool Valid, string? Error, string? Description, IReadOnlyList<DateTimeOffset> NextRuns);
