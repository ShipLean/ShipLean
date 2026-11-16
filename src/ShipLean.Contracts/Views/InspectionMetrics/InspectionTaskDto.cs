namespace ShipLean.Contracts.Views.InspectionMetrics;

// One measured task: where it sits, who did it (the agent connection's name when an agent started it), its start and end, where
// the end came from, and its lead time split into active, blocked and rework (adding up to LeadHours) plus the uncounted tail.
// PeriodId is the sprint its lead time ended in.
public sealed record InspectionTaskDto(
    string Key,
    string Title,
    string? StoryKey,
    string? FeatureKey,
    string? FeatureTitle,
    string Who,
    bool Agent,
    DateTimeOffset StartedAt,
    string StartedLabel,
    DateTimeOffset EndedAt,
    string EndedLabel,
    LeadTimeEndedBy EndedBy,
    double LeadHours,
    string LeadLabel,
    double ActiveHours,
    double BlockedHours,
    double ReworkHours,
    double UnresolvedTailHours,
    Guid? PeriodId);
