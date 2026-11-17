namespace ShipLean.Contracts.Views.InspectionMetrics;

// The tokens of one shown period (CK2), by the period's id (a sprint id).
public sealed record InspectionPeriodCostDto(Guid PeriodId, InspectionCostStatsDto Stats);
