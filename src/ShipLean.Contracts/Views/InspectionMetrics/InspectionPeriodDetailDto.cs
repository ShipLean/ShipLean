namespace ShipLean.Contracts.Views.InspectionMetrics;

// One sprint opened (?period=): its calendar quarter, its neighbours over all the project's started sprints (not only the range;
// null at either end), its numbers, its tasks slowest first, its Features, and the selected task's measurement. Selected is null
// unless select names one of its tasks: an unknown or foreign key is not an error, it just selects nothing.
public sealed record InspectionPeriodDetailDto(
    Guid PeriodId,
    string Name,
    string DatesLabel,
    string Quarter,
    InspectionPeriodRefDto? Previous,
    InspectionPeriodRefDto? Next,
    bool InProgress,
    LeadTimeStatsDto LeadTime,
    IReadOnlyList<InspectionTaskDto> Tasks,
    IReadOnlyList<InspectionPeriodFeatureDto> Features,
    InspectionTaskMeasurementDto? Selected);
