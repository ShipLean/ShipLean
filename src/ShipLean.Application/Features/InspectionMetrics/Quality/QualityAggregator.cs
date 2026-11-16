using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Core;

namespace ShipLean.Application.Features.InspectionMetrics.Quality;

// Escaping bugs on the lead-time view (QK3, QK5, design "Escaping bugs"). Each bug counts in one started sprint: the one its Caused
// by Task's lead time ended in (design D11), or, without a cause or when the cause has no measurable end in a started sprint, the
// one it was filed in. PRs count in the sprint they merged in. Both use InspectionPeriods.SprintOf, weekend grace included. A period,
// a headlined sprint and the opened period sum their own sprint; a quarter sums every sprint ending in it (its PeriodIds); the range
// sums the shown sprints. With no live bug in the project at all, the view is returned as it is, every Quality and Bugs null.
public sealed class QualityAggregator(ITaskLeadTimeMeasure measure) : IQualityAggregator
{
    private const double PerHundred = 100.0;

    public InspectionMetricsViewDto Apply(
        InspectionMetricsViewDto view,
        InspectionSprints sprints,
        InspectionWindow window,
        BugFacts facts,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        if (!facts.AnyBugs)
        {
            return view;
        }

        var started = InspectionPeriods.Started(sprints.Sprints, DisplayDate.Today(now, zone));
        var causeSprints = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var task in facts.Causes.Tasks)
        {
            if (measure.Measure(task, facts.Causes) is { } lead && InspectionPeriods.SprintOf(started, lead.End, zone) is { } sprint)
            {
                causeSprints[task.Key] = sprint.Id;
            }
        }
        var causeTitles = facts.Causes.Tasks.ToDictionary(task => task.Key, task => task.Title, StringComparer.Ordinal);

        var counted = facts.Bugs
            .Select(bug => (Bug: bug, PeriodId: bug.CausedByKey is { } cause && causeSprints.TryGetValue(cause, out var causeSprint)
                ? causeSprint
                : InspectionPeriods.SprintOf(started, bug.FiledAt, zone)?.Id))
            .Where(item => item.PeriodId is not null)
            .Select(item => new InspectionBugDto(
                item.Bug.Key,
                item.Bug.Title,
                item.Bug.Severity,
                item.Bug.FiledAt,
                DisplayDate.DayTime(item.Bug.FiledAt, zone),
                item.Bug.FoundBeforeRelease,
                item.Bug.CausedByKey,
                item.Bug.CausedByKey is { } key && causeTitles.TryGetValue(key, out var title) ? title : null,
                item.PeriodId!.Value))
            .OrderBy(bug => bug.FiledAt)
            .ThenBy(bug => bug.Key, StringComparer.Ordinal)
            .ToList();
        var prSprints = facts.MergedPrs.Select(pr => InspectionPeriods.SprintOf(started, pr.MergedAt, zone)?.Id).OfType<Guid>().ToList();

        IReadOnlyList<InspectionBugDto> BugsIn(IReadOnlyCollection<Guid> periodIds) => [.. counted.Where(bug => periodIds.Contains(bug.PeriodId))];

        InspectionQualityStatsDto Stats(IReadOnlyCollection<Guid> periodIds)
            => StatsOf(BugsIn(periodIds), prSprints.Count(periodIds.Contains), facts.SeverityWeights);

        var headline = view.Headline;
        Guid[] shown = [.. window.Shown.Select(sprint => sprint.Id)];
        return view with
        {
            Headline = headline with
            {
                Finished = headline.Finished is { } finished ? finished with { Quality = Stats([finished.PeriodId]) } : null,
                SoFar = headline.SoFar is { } soFar ? soFar with { Quality = Stats([soFar.PeriodId]) } : null,
                QuarterToDate = headline.QuarterToDate is { } toDate ? toDate with { Quality = Stats(toDate.PeriodIds) } : null,
                LastQuarter = headline.LastQuarter is { } lastQuarter ? lastQuarter with { Quality = Stats(lastQuarter.PeriodIds) } : null,
            },
            Periods = [.. view.Periods.Select(period => period with { Quality = Stats([period.Id]) })],
            Quarters = [.. view.Quarters.Select(quarter => quarter with { Quality = Stats(quarter.PeriodIds) })],
            PeriodDetail = view.PeriodDetail is { } detail
                ? detail with { Quality = Stats([detail.PeriodId]), Bugs = BugsIn([detail.PeriodId]) }
                : null,
            Quality = Stats(shown),
            Bugs = BugsIn(shown),
        };
    }

    // One span's numbers over the bugs counted in it and the PRs merged in it. A bug with no severity, or a severity with no weight,
    // weighs 0. MergedPrs and Per100Prs are null when no PR merged in the span.
    private static InspectionQualityStatsDto StatsOf(IReadOnlyList<InspectionBugDto> bugs, int mergedPrs, IReadOnlyDictionary<int, int> weights)
    {
        var escaping = bugs.Where(bug => !bug.FoundBeforeRelease).ToList();
        var weighted = escaping.Sum(bug => bug.Severity is { } severity && weights.TryGetValue(severity, out var weight) ? weight : 0);
        int? prs = mergedPrs == 0 ? null : mergedPrs;
        double? per100 = prs is { } count ? Math.Round(escaping.Count * PerHundred / count, 1, MidpointRounding.AwayFromZero) : null;
        return new InspectionQualityStatsDto(
            escaping.Count,
            weighted,
            bugs.Count - escaping.Count,
            prs,
            per100,
            new InspectionSeverityCountsDto(
                escaping.Count(bug => bug.Severity == 1),
                escaping.Count(bug => bug.Severity == 2),
                escaping.Count(bug => bug.Severity == 3),
                escaping.Count(bug => bug.Severity == 4),
                escaping.Count(bug => bug.Severity is not (1 or 2 or 3 or 4))));
    }
}
