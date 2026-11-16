using ShipLean.Application.Features.InspectionMetrics.Aggregate;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Core;

namespace ShipLean.Application.Features.InspectionMetrics;

// A project's Inspection lead time (K2, K3): member access first, then the query is validated against the project's sprints, the
// range becomes a window, the facts in that window are read, each task is measured and the aggregator builds the view in the
// viewer's zone.
public sealed class InspectionMetricsView(
    IProjectAccess access,
    InspectionSprintsReader sprintsReader,
    ILeadTimeFactsReader factsReader,
    ITaskLeadTimeMeasure measure,
    ILeadTimeAggregator aggregator,
    TimeProvider clock,
    IViewerZone viewerZone) : IInspectionMetrics
{
    public async Task<InspectionMetricsViewDto> GetAsync(Guid projectId, InspectionMetricsQuery query, CancellationToken ct)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, ct);
        var sprints = await sprintsReader.ReadAsync(projectId, ct);
        var selection = InspectionMetricsSelection.Parse(query, sprints.Sprints);

        var now = clock.GetUtcNow();
        var zone = viewerZone.Zone;
        var window = InspectionWindow.For(selection.Range, sprints.Sprints, DisplayDate.Today(now, zone), selection.Period);
        var facts = await factsReader.ReadAsync(projectId, window.From, window.To, ct);
        var measured = facts.Tasks.Select(task => measure.Measure(task, facts)).OfType<TaskLeadTime>().ToList();
        return aggregator.Build(sprints, window, facts, measured, selection, now, zone);
    }
}
