namespace ShipLean.Contracts.Views.InspectionMetrics;

// One headlined sprint, its lead-time numbers and its escaping bugs (QK3: null when the project has no bugs).
public sealed record InspectionHeadlinePeriodDto(Guid PeriodId, string Name, LeadTimeStatsDto LeadTime, InspectionQualityStatsDto? Quality);
