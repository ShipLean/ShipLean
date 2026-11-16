namespace ShipLean.Contracts.Inspection;

// Normalised (K3): Kind is internal|server|client|producer|consumer, attribute values are strings, resource attributes
// are merged in with the span's own winning, times are UTC at microsecond precision.
public sealed record SpanRecord(
    string TraceId,
    string SpanId,
    string? ParentSpanId,
    string Name,
    string Kind,
    DateTimeOffset Start,
    DateTimeOffset End,
    SpanStatus Status,
    string? StatusMessage,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<SpanEventData> Events);
