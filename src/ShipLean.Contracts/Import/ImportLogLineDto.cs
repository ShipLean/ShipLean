namespace ShipLean.Contracts.Import;

// TimeLabel: the time of day the line was logged ("14:02:05").
public sealed record ImportLogLineDto(long Id, DateTimeOffset LoggedAt, string TimeLabel, ImportLogLevel Level, string Message);
