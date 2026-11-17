namespace ShipLean.Contracts.Views.InspectionMetrics;

// The tokens of one quarter (CK2), by the quarter's label ("Q4 2026"), summing every started sprint ending in it.
public sealed record InspectionQuarterCostDto(string Quarter, InspectionCostStatsDto Stats);
