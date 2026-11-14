namespace ShipLean.Contracts.Views.Timeline;

// The chart's time axis. Start is the UTC instant of the viewer-zone midnight that starts the first day drawn; every other
// position is hours from it. EndHours is where the last day ends; NowHours is null once the Feature is finished.
public sealed record TimelineScaleDto(
    DateTimeOffset Start,
    double EndHours,
    double? NowHours,
    IReadOnlyList<TimelineDayDto> Days,
    IReadOnlyList<TimelineSprintDto> Sprints);
