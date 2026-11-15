using ShipLean.Application.Features.Timeline.Build;
using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline;

// The pure builder (C3): bars, then edges over those bars, then the composition marks the bars on the critical path and adds the
// page's address. No I/O.
public static class TimelineBuilder
{
    public static FeatureTimelineViewDto Build(TimelineFacts facts, DateTimeOffset asOf, TimeZoneInfo zone)
    {
        var bars = TimelineBars.Build(facts, asOf, zone);
        var edges = TimelineEdges.Build(facts, bars, bars.Finished);
        var critical = edges.CriticalPath.ToHashSet(StringComparer.Ordinal);
        return new FeatureTimelineViewDto(
            bars.Feature,
            asOf,
            bars.Finished,
            bars.KeyDates,
            bars.Gaps,
            bars.Execution,
            bars.Scale,
            edges.CriticalPath,
            bars.Stories,
            [.. bars.Bars.Select(bar => bar with { OnCriticalPath = critical.Contains(bar.Key) })],
            edges.Edges,
            bars.NotStarted,
            new TimelineLinksDto($"/p/{facts.ProjectKey}/items/{bars.Feature.Key}/timeline"));
    }
}
