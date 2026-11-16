namespace ShipLean.Contracts.Views.InspectionMetrics;

// One headlined sprint, its lead-time numbers and its escaping bugs (QK3: null exactly when the project has no live bug at all).
public sealed record InspectionHeadlinePeriodDto(Guid PeriodId, string Name, LeadTimeStatsDto LeadTime, InspectionQualityStatsDto? Quality);
