namespace ShipLean.Contracts.Analytics;

// One event as a sink sends it. Properties is flat: the AnalyticsProps standard keys (a key is left out when its value is null)
// plus the event's extra properties. Values are ids, enum-like strings, numbers and booleans, never free text.
public sealed record AnalyticsEvent(
    string Name,
    string DistinctId,
    string ActorKind,
    string? ProjectKey,
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, object> Properties,
    AnalyticsPerson Person);
