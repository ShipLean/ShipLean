namespace ShipLean.Contracts.Inspection;

public sealed record SpanEventData(DateTimeOffset Time, string Name, IReadOnlyDictionary<string, string> Attributes);
