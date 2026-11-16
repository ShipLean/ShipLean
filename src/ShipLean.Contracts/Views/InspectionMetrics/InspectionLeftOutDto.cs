namespace ShipLean.Contracts.Views.InspectionMetrics;

// A task closed in the range with no start recorded (design D9): never given a made-up start. At is when it closed.
public sealed record InspectionLeftOutDto(string Key, string Title, string? FeatureKey, string Reason, DateTimeOffset At, string AtLabel);
