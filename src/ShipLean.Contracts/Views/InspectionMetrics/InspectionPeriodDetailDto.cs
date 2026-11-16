namespace ShipLean.Contracts.Views.InspectionMetrics;

// One sprint opened (?period=): its calendar quarter, its neighbours over all the project's started sprints (not only the range;
// null at either end), its numbers, its tasks slowest first, its Features, and the selected task's measurement. Without select,
// Selected is the slowest task (the first of Tasks; null with no tasks); with select naming one of its tasks, that task. An unknown
// or foreign key is not an error: Selected is null.
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
