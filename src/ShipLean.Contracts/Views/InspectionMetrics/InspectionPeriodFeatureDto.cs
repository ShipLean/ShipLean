namespace ShipLean.Contracts.Views.InspectionMetrics;

// How much of a Feature ended in the sprint: its tasks whose lead time ended here against all its measured tasks, and their mean.
public sealed record InspectionPeriodFeatureDto(
    string Key,
    string Title,
    int TasksEnded,
    int TasksTotal,
    double? MeanLeadHours,
    string? MeanLeadLabel);
