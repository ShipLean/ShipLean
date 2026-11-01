namespace ShipLean.Contracts.Analytics;

// GET and PUT /api/instance/analytics. Host and SiteKey are null when the provider is none.
public sealed record AnalyticsSettingsDto(AnalyticsProvider Provider, string? Host, string? SiteKey, bool SendNames);
