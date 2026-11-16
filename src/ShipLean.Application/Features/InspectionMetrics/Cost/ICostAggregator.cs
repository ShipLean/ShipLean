using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Cost;

// Builds the view's Cost (CK2, CK3) from the view lead time and quality built, the project's sprints, the window, the facts, the
// measured tasks, the tokens per task the store found and the merged PRs per sprint (the quality wave's MergedPrs, placed with
// InspectionPeriods.SprintOf). A task counts in the sprint its lead time ended in (InspectionPeriods.SprintOf of its end) and its
// Who is TaskPlacement.Who; quarters sum every started sprint ending in them, shown or not. With tokens.Available false the cost is
// unavailable: tokens.Problem as its Note, empty lists and null stats.
public interface ICostAggregator
{
    InspectionCostDto Build(
        InspectionMetricsViewDto view,
        InspectionSprints sprints,
        InspectionWindow window,
        LeadTimeFacts facts,
        IReadOnlyList<TaskLeadTime> tasks,
        TaskTokenTotals tokens,
        IReadOnlyDictionary<Guid, int> mergedPrsBySprint,
        DateTimeOffset now,
        TimeZoneInfo zone);
}
