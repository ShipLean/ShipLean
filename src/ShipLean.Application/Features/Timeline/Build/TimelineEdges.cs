using ShipLean.Application.Features.Timeline.Facts;

namespace ShipLean.Application.Features.Timeline.Build;

// Turns the facts' links and waves into arrows between the drawn bars, and finds the critical path once finished. Pure.
public static class TimelineEdges
{
    // TODO: returns no edges and no critical path; the edges builder (link and wave edges, blocked edges, wait labels, critical
    // path) replaces this.
    public static TimelineEdgesPart Build(TimelineFacts facts, TimelineBarsPart bars, bool finished) => new([], []);
}
