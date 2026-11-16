using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Measure;

// One task measured (K6). Lead time runs from Start to End; Active, Blocked and Rework (hours, unrounded) add up to it; the
// unresolved tail (last commit to Resolved) is not counted. Start is null only for a left-out task, with LeftOutReason set.
public sealed record TaskLeadTime(
    string TaskKey,
    DateTimeOffset? Start,
    DateTimeOffset End,
    LeadTimeEndedBy EndedBy,
    double ActiveHours,
    double BlockedHours,
    double ReworkHours,
    double UnresolvedTailHours,
    IReadOnlyList<LeadTimeSegment> Segments,
    IReadOnlyList<LeadTimeEvent> Events,
    string? LeftOutReason);
