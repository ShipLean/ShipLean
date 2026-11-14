using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline.Build;

// What TimelineBars makes (C3): the header, key dates, gaps, execution, scale, story brackets, task bars (OnCriticalPath still
// false: the composition sets it) and the items that never started. Finished says whether the Feature is finished.
public sealed record TimelineBarsPart(
    TimelineFeatureDto Feature,
    bool Finished,
    IReadOnlyList<TimelineKeyDateDto> KeyDates,
    IReadOnlyList<TimelineGapDto> Gaps,
    TimelineExecutionDto Execution,
    TimelineScaleDto Scale,
    IReadOnlyList<TimelineStoryDto> Stories,
    IReadOnlyList<TimelineBarDto> Bars,
    IReadOnlyList<TimelineNotStartedDto> NotStarted);
