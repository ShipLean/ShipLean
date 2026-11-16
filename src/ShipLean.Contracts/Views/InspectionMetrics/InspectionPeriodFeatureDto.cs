namespace ShipLean.Contracts.Views.InspectionMetrics;

// How much of a Feature ended in the sprint: its tasks whose lead time ended here, those that ended in earlier sprints, and all its
// tasks ("2 of 8 · 1 before"); the lead-time numbers of the tasks that ended here (the split bar); its longest task here with its
// lead label; and how long the Feature has been executing ("11 days so far"), null when it hasn't started.
public sealed record InspectionPeriodFeatureDto(
    string Key,
    string Title,
    int TasksEnded,
    int TasksEndedBefore,
    int TasksTotal,
    LeadTimeStatsDto LeadTime,
    string? LongestTaskKey,
    string? LongestLeadLabel,
    string? ExecutionLabel);
