using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline.Build;

// What TimelineEdges makes (C3): the arrows between this Feature's items, and the critical path in order (empty unless finished).
public sealed record TimelineEdgesPart(IReadOnlyList<TimelineEdgeDto> Edges, IReadOnlyList<string> CriticalPath);
