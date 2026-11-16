using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Measure;

// A stretch of a measured task's time.
public sealed record LeadTimeSegment(InspectionSegmentKind Kind, DateTimeOffset From, DateTimeOffset To);
