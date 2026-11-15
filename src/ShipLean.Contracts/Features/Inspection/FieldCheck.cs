namespace ShipLean.Contracts.Inspection;

// One K1 field: its name, the attribute it maps to, and whether recent data carries it.
public sealed record FieldCheck(string Name, string Attribute, bool Found);
