namespace ShipLean.Contracts.Views.Timeline;

// One period of a task's history. StateName is the workflow state's own name (a Blocked period is still in Active). The period
// a task ends in (resolved or closed) has To, Hours and DurationLabel null; an open period runs to AsOf.
public sealed record TimelinePeriodDto(
    TimelinePeriodState State,
    string StateName,
    DateTimeOffset From,
    DateTimeOffset? To,
    string FromLabel,
    double? Hours,
    string? DurationLabel);
