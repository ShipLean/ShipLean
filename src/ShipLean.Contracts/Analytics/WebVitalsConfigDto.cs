namespace ShipLean.Contracts.Analytics;

// GET /api/usage/web-vitals/config. Enabled is true only when a provider is set and the web vitals switch is on.
public sealed record WebVitalsConfigDto(bool Enabled);
