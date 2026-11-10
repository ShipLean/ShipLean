namespace ShipLean.Contracts.Screens;

// One group of the Features overview. Rows is empty when Folded; Count is the number of Features in the group either way.
public sealed record FeatureGroupDto(string StateCategory, string Label, bool Folded, int Count, IReadOnlyList<FeatureRowDto> Rows);
