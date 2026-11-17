using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Cost;

public sealed class CostAggregator : ICostAggregator
{
    public const string NotBuilt = "Tokens per task are not added up yet.";

    // TODO: a stub until the cost aggregation lands; it reports the cost unavailable with empty lists and no stats.
    public InspectionCostDto Build(
        InspectionMetricsViewDto view,
        InspectionSprints sprints,
        InspectionWindow window,
        LeadTimeFacts facts,
        IReadOnlyList<TaskLeadTime> tasks,
        TaskTokenTotals tokens,
        IReadOnlyDictionary<Guid, int> mergedPrsBySprint,
        DateTimeOffset now,
        TimeZoneInfo zone)
        => new(false, NotBuilt, [], [], null, new InspectionCostHeadlineDto(null, null, null, null), [], []);
}
