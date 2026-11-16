using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views.Timeline;

// One task's bar (D2, D5): from its first move to Active to its last move to Resolved, from state history only. End is null
// while InFlight. Inferred is a task closed with no start recorded (D4): Start is null and it is drawn as a diamond at End.
// History is every state period, Blocked periods from the Blocked tag (D16); Reviews are review events (D15), drawn after the bar.
public sealed record TimelineBarDto(
    string Key,
    WorkItemTypeId Type,
    string Title,
    string Parent,
    TimelinePersonDto? Assignee,
    TimelineBarState State,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    double? StartHours,
    double? EndHours,
    double? Hours,
    string? StartLabel,
    string? EndLabel,
    string? DurationLabel,
    bool Inferred,
    bool OnCriticalPath,
    IReadOnlyList<TimelinePeriodDto> History,
    IReadOnlyList<TimelineReviewDto> Reviews);
