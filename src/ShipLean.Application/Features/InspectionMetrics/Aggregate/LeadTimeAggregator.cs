using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Views.InspectionMetrics;

namespace ShipLean.Application.Features.InspectionMetrics.Aggregate;

public sealed class LeadTimeAggregator : ILeadTimeAggregator
{
    public const string Caveat = "These numbers tell you where to look, not what caused a change.";

    // TODO: build the periods, headline, quarters, cuts, longest, left out and period detail; until then only the fixed parts.
    public InspectionMetricsViewDto Build(
        InspectionSprints sprints,
        InspectionWindow window,
        LeadTimeFacts facts,
        IReadOnlyList<TaskLeadTime> tasks,
        InspectionMetricsSelection selection,
        DateTimeOffset now,
        TimeZoneInfo zone)
        => new(
            sprints.Project,
            InspectionCadence.Sprints,
            selection.Range,
            selection.Cut,
            InspectionAggregate.Mean,
            now,
            Caveat,
            new InspectionCoverageDto(0, 0, null),
            new InspectionHeadlineDto(null, null),
            [],
            [],
            selection.Cut == InspectionCut.None ? null : [],
            [],
            [],
            null);
}
