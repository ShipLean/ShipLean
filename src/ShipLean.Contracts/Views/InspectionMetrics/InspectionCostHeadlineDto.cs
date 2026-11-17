namespace ShipLean.Contracts.Views.InspectionMetrics;

// The Cost card's numbers (CK2), matching the lead-time headline: the last finished sprint, the running one so far, this quarter to
// date and last quarter; each null when the lead-time headline has none.
public sealed record InspectionCostHeadlineDto(
    InspectionCostStatsDto? Finished,
    InspectionCostStatsDto? SoFar,
    InspectionCostStatsDto? QuarterToDate,
    InspectionCostStatsDto? LastQuarter);
