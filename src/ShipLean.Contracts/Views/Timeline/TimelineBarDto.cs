using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views.Timeline;

// One task's bar (D2, D5): from its first move to Active to its last move to Resolved, from state history only. End is null
// while InFlight. Inferred is a task closed with no start recorded (D4): Start is null and it is drawn as a diamond at End.
// History is every state period, Blocked periods from the Blocked tag (D16); Reviews are review events (D15), drawn after the bar.
// LastCommitAt is the time of the task's last commit when one has a recorded time (SHIP-1398 K8), LastCommitHours its hour offset
// from scale.start and LastCommitLabel its label made with DisplayDate; all three are null when no commit time is known.
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
    IReadOnlyList<TimelineReviewDto> Reviews,
    DateTimeOffset? LastCommitAt,
    double? LastCommitHours,
    string? LastCommitLabel);
