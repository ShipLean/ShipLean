namespace ShipLean.Contracts.Inspection;

public sealed record StoreHealth(bool Reachable, int? LatencyMs, int? ViewLagSeconds, string? Problem);
