namespace ShipLean.Contracts.Views.InspectionMetrics;

// One row of the cut table: a Feature (Key its item key) or, for who, an agent connection (Key its name) or people together
// (Key "people", Agent false). Cells has one entry per shown period, in Periods order; All is across the range.
public sealed record InspectionCutRowDto(string Key, string Label, bool Agent, IReadOnlyList<LeadTimeStatsDto> Cells, LeadTimeStatsDto All);
