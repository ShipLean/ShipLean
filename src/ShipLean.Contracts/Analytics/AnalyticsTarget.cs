namespace ShipLean.Contracts.Analytics;

// Where and how a batch is sent: the saved provider settings plus what every event needs to say about this instance.
public sealed record AnalyticsTarget(AnalyticsProvider Provider, Uri Host, string SiteKey, string InstanceHost, bool SendNames, string ShipLeanVersion);
