using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Quality;

// Adds escaping bugs (QK3, QK5) to the lead-time view the lead-time aggregator built: Quality on the view (the shown sprints), on
// each period, each quarter and each headlined sprint, and Quality and Bugs on the opened period. Every one of them stays null when
// facts.Bugs is empty (the project has no bugs: the page shows "No bugs yet"); otherwise every one is set. A bug counts in the
// sprint it was filed in, or, with a Caused by link, the sprint its cause's lead time ended in, measured with ITaskLeadTimeMeasure
// (design D11); the sprint of an instant and the quarter of a sprint come from Periods/InspectionPeriods, as lead time uses them.
public interface IQualityAggregator
{
    InspectionMetricsViewDto Apply(
        InspectionMetricsViewDto view,
        InspectionSprints sprints,
        InspectionWindow window,
        BugFacts facts,
        DateTimeOffset now,
        TimeZoneInfo zone);
}
