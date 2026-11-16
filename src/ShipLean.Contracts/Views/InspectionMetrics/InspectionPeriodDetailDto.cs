namespace ShipLean.Contracts.Views.InspectionMetrics;

// One sprint opened (?period=): its numbers, its tasks slowest first, its Features, and the selected task's measurement (null
// unless select names one of its tasks).
public sealed record InspectionPeriodDetailDto(
    Guid PeriodId,
    string Name,
    string DatesLabel,
    bool InProgress,
    LeadTimeStatsDto LeadTime,
    IReadOnlyList<InspectionTaskDto> Tasks,
    IReadOnlyList<InspectionPeriodFeatureDto> Features,
    InspectionTaskMeasurementDto? Selected);
