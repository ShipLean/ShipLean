using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Quality;

// TODO: a stub that leaves every Quality and Bugs null; the real aggregator replaces it.
public sealed class QualityAggregator : IQualityAggregator
{
    public InspectionMetricsViewDto Apply(
        InspectionMetricsViewDto view,
        InspectionSprints sprints,
        InspectionWindow window,
        BugFacts facts,
        DateTimeOffset now,
        TimeZoneInfo zone)
        => view;
}
