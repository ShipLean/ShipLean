namespace ShipLean.Contracts.Views.InspectionMetrics;

// One group of tasks' lead-time numbers: means in hours to one decimal (null with no tasks), each with its server label. The
// active, blocked and rework means add up to the lead mean; the unresolved tail is shown, not counted. ChangePctVsPrevious is the
// whole-percent change against the previous sprint (null with no previous or none to compare). EndedAtResolved counts the tasks
// with no commit time.
public sealed record LeadTimeStatsDto(
    int Tasks,
    double? MeanHours,
    string? MeanLabel,
    double? MeanActiveHours,
    string? MeanActiveLabel,
    double? MeanBlockedHours,
    string? MeanBlockedLabel,
    double? MeanReworkHours,
    string? MeanReworkLabel,
    double? MeanUnresolvedTailHours,
    int EndedAtResolved,
    int? ChangePctVsPrevious);
