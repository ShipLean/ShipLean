namespace ShipLean.Contracts.Persisted;

public sealed record StyleRuleJson(string Id, string Name, bool Enabled, Swatch? Colour, bool Bold, bool Italic, bool Underline, CriteriaJson Criteria);
