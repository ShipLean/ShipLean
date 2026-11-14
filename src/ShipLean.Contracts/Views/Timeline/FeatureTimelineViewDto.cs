namespace ShipLean.Contracts.Views.Timeline;

// GET of a Feature's timeline, and the MCP tool feature_timeline's result (D11, D17): a Gantt chart of the Feature's delivery,
// drawn after the fact from state history. Every instant is a UTC ISO string; every position is an hour offset from Scale.Start
// and every label is made on the server in the viewer's zone, so the browser does no date maths. CriticalPath is empty unless
// Finished. NotStarted lists the tasks, stories and bugs that never left New and were never closed.
public sealed record FeatureTimelineViewDto(
    TimelineFeatureDto Feature,
    DateTimeOffset AsOf,
    bool Finished,
    IReadOnlyList<TimelineKeyDateDto> KeyDates,
    IReadOnlyList<TimelineGapDto> Gaps,
    TimelineExecutionDto Execution,
    TimelineScaleDto Scale,
    IReadOnlyList<string> CriticalPath,
    IReadOnlyList<TimelineStoryDto> Stories,
    IReadOnlyList<TimelineBarDto> Bars,
    IReadOnlyList<TimelineEdgeDto> Edges,
    IReadOnlyList<TimelineNotStartedDto> NotStarted,
    TimelineLinksDto Links);
