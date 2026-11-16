namespace ShipLean.Contracts.Inspection;

public sealed record RunMarker(MarkerKind Kind, DateTimeOffset At, DateTimeOffset? Until, string? Label);
