namespace ShipLean.Contracts.Analytics;

// The actor behind an event, as the provider records it (Umami identify data, PostHog $set).
public sealed record AnalyticsPerson(string DistinctId, IReadOnlyDictionary<string, object> Properties);
