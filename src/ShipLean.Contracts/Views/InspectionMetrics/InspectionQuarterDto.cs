namespace ShipLean.Contracts.Views.InspectionMetrics;

// A calendar quarter's roll-up ("Q4 2026") over every sprint ending in it, shown or not (design D7). ToDate when it holds today.
public sealed record InspectionQuarterDto(string Quarter, bool ToDate, IReadOnlyList<Guid> PeriodIds, LeadTimeStatsDto LeadTime);
