namespace ShipLean.Contracts.Persisted;

public sealed record SwimlaneJson(string Id, string Name, Swatch? Colour, SwimlaneKind Kind, CriteriaJson? Criteria);
