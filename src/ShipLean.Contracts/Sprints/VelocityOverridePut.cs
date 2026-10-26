namespace ShipLean.Contracts.Sprints;

// The velocity chip's override; null clears it (back to the average).
public sealed record VelocityOverridePut(decimal? VelocityOverride);
