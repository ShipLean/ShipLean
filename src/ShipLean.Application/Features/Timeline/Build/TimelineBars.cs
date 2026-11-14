using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline.Build;

// Turns the facts into the header, key dates, gaps, execution, scale, story brackets and task bars, in the viewer's zone. Pure.
public static class TimelineBars
{
    // TODO: returns the Feature header with everything else empty; the bars builder (key dates, gaps, execution, scale, brackets,
    // bars, not started, finished) replaces this.
    public static TimelineBarsPart Build(TimelineFacts facts, DateTimeOffset asOf, TimeZoneInfo zone)
    {
        var feature = facts.Feature;
        var header = new TimelineFeatureDto(
            feature.Id, feature.Key, feature.Title, feature.StateName, feature.StateCategory,
            feature.Epic is null ? null : new TimelineEpicDto(feature.Epic.Key, feature.Epic.Title));
        return new TimelineBarsPart(
            header, false, [], [], new TimelineExecutionDto(null, null, null), new TimelineScaleDto(asOf, 0, null, [], []), [], [], []);
    }
}
