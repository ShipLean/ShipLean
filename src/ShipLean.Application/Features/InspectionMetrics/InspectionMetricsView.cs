using ShipLean.Application.Features.InspectionMetrics.Aggregate;
using ShipLean.Application.Features.InspectionMetrics.Cost;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Application.Features.InspectionMetrics.Quality;
using ShipLean.Contracts;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Core;

namespace ShipLean.Application.Features.InspectionMetrics;

// A project's Inspection lead time (K2, K3): member access first, then the query is validated against the project's sprints, the
// range becomes a window, the facts in that window are read, each task is measured and the aggregator builds the view in the
// viewer's zone; then the quality facts over the same days are read and the quality aggregator adds escaping bugs to it (QK3-QK5).
// Last comes cost (CK3): null when Inspection (agent runs) is off for the instance or the project, with no token read; otherwise the
// tokens of the measured tasks whose lead time ended in a sprint the window touches, linked from their earliest start until now, and
// the quality facts' merged PRs placed in their sprints.
public sealed class InspectionMetricsView(
    IProjectAccess access,
    InspectionSprintsReader sprintsReader,
    ILeadTimeFactsReader factsReader,
    ITaskLeadTimeMeasure measure,
    ILeadTimeAggregator aggregator,
    IQualityFactsReader qualityReader,
    IQualityAggregator qualityAggregator,
    IInspectionFlag inspectionFlag,
    ITaskTokenTotals tokenTotals,
    ICostAggregator costAggregator,
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
        var view = aggregator.Build(sprints, window, facts, measured, selection, now, zone);
        var bugs = await qualityReader.ReadAsync(projectId, window.From, window.To, ct);
        view = qualityAggregator.Apply(view, sprints, window, bugs, now, zone);
        if (!await inspectionFlag.IsOnAsync(projectId, ct))
        {
            return view;
        }

        var started = InspectionPeriods.Started(sprints.Sprints, DisplayDate.Today(now, zone));
        var touched = started.Where(sprint => sprint.EndDate >= window.From && sprint.StartDate <= window.To).Select(sprint => sprint.Id).ToHashSet();
        var placed = measured
            .Where(task => task.Start is not null && InspectionPeriods.SprintOf(started, task.End, zone) is { } sprint && touched.Contains(sprint.Id))
            .ToList();
        var from = placed.Count == 0 ? now : placed.Min(task => task.Start!.Value);
        var tokens = await tokenTotals.ReadAsync(new TaskTokenQuery(view.Project.Key, [.. placed.Select(task => task.TaskKey)], from, now), ct);
        var mergedPrsBySprint = bugs.MergedPrs
            .Select(pr => InspectionPeriods.SprintOf(started, pr.MergedAt, zone)?.Id)
            .OfType<Guid>()
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());
        return view with { Cost = costAggregator.Build(view, sprints, window, facts, measured, tokens, mergedPrsBySprint, now, zone) };
    }
}
