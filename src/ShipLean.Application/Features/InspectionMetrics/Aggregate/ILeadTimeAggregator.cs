using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Aggregate;

// Builds the view (K7) from the project's sprints, the window, the facts and the measured tasks, as of now in the viewer's zone.
public interface ILeadTimeAggregator
{
    InspectionMetricsViewDto Build(
        InspectionSprints sprints,
        InspectionWindow window,
        LeadTimeFacts facts,
        IReadOnlyList<TaskLeadTime> tasks,
        InspectionMetricsSelection selection,
        DateTimeOffset now,
        TimeZoneInfo zone);
}
