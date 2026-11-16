namespace ShipLean.Contracts.Views.InspectionMetrics;

// A calendar quarter's roll-up ("Q4 2026") over every sprint ending in it, shown or not (design D7). ToDate when it holds today.
// LeadTime.ChangePctVsPrevious compares with last quarter: the whole of it for a finished quarter, and for the quarter to date only
// last quarter's tasks that ended up to the same day into it. Quality sums the same sprints (QK3: null exactly when the project has no live bug at all).
public sealed record InspectionQuarterDto(string Quarter, bool ToDate, IReadOnlyList<Guid> PeriodIds, LeadTimeStatsDto LeadTime, InspectionQualityStatsDto? Quality);
