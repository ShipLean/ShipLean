namespace ShipLean.Contracts.Views.InspectionMetrics;

// One shown sprint, oldest first: its days, calendar quarter (by end date), whether it is running (drawn dashed, so far), its
// lead-time numbers over the tasks whose lead time ended in it, and a dot per such task for the chart.
public sealed record InspectionPeriodDto(
    Guid Id,
    string Name,
    string ShortName,
    DateOnly StartDate,
    DateOnly EndDate,
    string DatesLabel,
    string Quarter,
    bool InProgress,
    LeadTimeStatsDto LeadTime,
    IReadOnlyList<InspectionDotDto> Dots);
