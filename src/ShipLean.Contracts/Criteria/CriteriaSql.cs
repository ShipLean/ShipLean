namespace ShipLean.Contracts.Criteria;

// Where holds named placeholders (@p0, @p1 ...) that Parameters fills; it never contains a value.
public sealed record CriteriaSql(string Where, IReadOnlyDictionary<string, object?> Parameters);
