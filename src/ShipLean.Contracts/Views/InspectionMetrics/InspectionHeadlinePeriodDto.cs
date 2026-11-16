namespace ShipLean.Contracts.Views.InspectionMetrics;

// One headlined sprint and its lead-time numbers.
public sealed record InspectionHeadlinePeriodDto(Guid PeriodId, string Name, LeadTimeStatsDto LeadTime);
