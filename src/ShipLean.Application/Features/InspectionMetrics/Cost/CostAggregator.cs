using ShipLean.Application.Features.InspectionMetrics.Aggregate;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Core;

namespace ShipLean.Application.Features.InspectionMetrics.Cost;

// The view's Cost (CK2, CK3), following ICostAggregator. A pure function of what it is given: no store, no clock but `now`.
public sealed class CostAggregator : ICostAggregator
{
    private const int MostTokensCount = 8;

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
    {
        if (!tokens.Available)
        {
            return new InspectionCostDto(false, tokens.Problem, [], [], null, new InspectionCostHeadlineDto(null, null, null, null), [], []);
        }
        return new Run(view, sprints, window, facts, tasks, tokens, mergedPrsBySprint, now, zone).Build();
    }

    // A measured task placed in the sprint its lead time ended in, with the tokens its runs counted (null when it had no run).
    private sealed record Placed(LeadTimeTaskFact Fact, TaskLeadTime Lead, Guid SprintId, string Who, TaskTokens? Tokens)
    {
        public double LeadHours => Math.Max(0, (Lead.End - (Lead.Start ?? Lead.End)).TotalHours);
    }

    private sealed class Run
    {
        private readonly InspectionMetricsViewDto _view;
        private readonly InspectionWindow _window;
        private readonly TaskTokenTotals _tokens;
        private readonly IReadOnlyDictionary<Guid, int> _mergedPrs;
        private readonly TimeZoneInfo _zone;
        private readonly IReadOnlyList<InspectionSprint> _started;
        private readonly ILookup<Guid, Placed> _bySprint;

        public Run(
            InspectionMetricsViewDto view,
            InspectionSprints sprints,
            InspectionWindow window,
            LeadTimeFacts facts,
            IReadOnlyList<TaskLeadTime> tasks,
            TaskTokenTotals tokens,
            IReadOnlyDictionary<Guid, int> mergedPrs,
            DateTimeOffset now,
            TimeZoneInfo zone)
        {
            _view = view;
            _window = window;
            _tokens = tokens;
            _mergedPrs = mergedPrs;
            _zone = zone;
            _started = InspectionPeriods.Started(sprints.Sprints, DisplayDate.Today(now, zone));
            var taskFacts = facts.Tasks.ToDictionary(task => task.Key, StringComparer.Ordinal);
            var starters = TaskPlacement.Starters(facts);
            _bySprint = tasks
                .Where(task => task.Start is not null)
                .Select(task =>
                {
                    var fact = taskFacts[task.TaskKey];
                    var sprint = InspectionPeriods.SprintOf(_started, task.End, zone);
                    var who = TaskPlacement.Who(fact, starters.GetValueOrDefault(fact.Key), facts.People);
                    return new Placed(fact, task, sprint?.Id ?? Guid.Empty, who, tokens.ByItem.GetValueOrDefault(fact.Key));
                })
                .Where(placed => placed.SprintId != Guid.Empty)
                .ToLookup(placed => placed.SprintId);
        }

        public InspectionCostDto Build()
        {
            var keptShown = _window.Shown.Where(Kept).ToList();
            var inRange = keptShown.SelectMany(TasksIn).Where(placed => placed.Tokens is not null).ToList();
            return new InspectionCostDto(
                true,
                null,
                [.. _view.Periods.Select(period => new InspectionPeriodCostDto(period.Id, PeriodStats(period.Id)))],
                [.. _view.Quarters.Select(quarter => new InspectionQuarterCostDto(quarter.Quarter, QuarterStats(quarter.PeriodIds)))],
                Stats(_window.Shown, null),
                new InspectionCostHeadlineDto(
                    HeadlinePeriod(_view.Headline.Finished),
                    HeadlinePeriod(_view.Headline.SoFar),
                    HeadlineQuarter(_view.Headline.QuarterToDate),
                    HeadlineQuarter(_view.Headline.LastQuarter)),
                [.. inRange
                    .OrderByDescending(placed => placed.Tokens!.Total)
                    .ThenBy(placed => placed.Fact.Key, StringComparer.Ordinal)
                    .Take(MostTokensCount)
                    .Select(TaskDto)],
                ByFeature(inRange));
        }

        private InspectionCostStatsDto? HeadlinePeriod(InspectionHeadlinePeriodDto? period) => period is null ? null : PeriodStats(period.PeriodId);

        private InspectionCostStatsDto? HeadlineQuarter(InspectionQuarterDto? quarter) => quarter is null ? null : QuarterStats(quarter.PeriodIds);

        private InspectionCostStatsDto PeriodStats(Guid sprintId)
        {
            var sprint = _started.First(candidate => candidate.Id == sprintId);
            var previous = InspectionPeriods.Previous(_started, sprint);
            return Stats([sprint], previous is null ? null : [previous]);
        }

        // A quarter against the whole previous quarter (the lead-time quarter rule also stops the previous one at the same day into it).
        private InspectionCostStatsDto QuarterStats(IReadOnlyList<Guid> sprintIds)
        {
            var sprints = _started.Where(sprint => sprintIds.Contains(sprint.Id)).ToList();
            var previous = sprints.Count == 0 ? [] : InspectionPeriods.SprintsEndingIn(_started, InspectionPeriods.QuarterOf(sprints[0]).AddMonths(-3));
            return Stats(sprints, previous.Count == 0 ? null : previous);
        }

        // The span's numbers over the tasks that ended in its sprints; against is the span the change is measured from.
        private InspectionCostStatsDto Stats(IReadOnlyList<InspectionSprint> sprints, IReadOnlyList<InspectionSprint>? against)
        {
            var tasks = sprints.SelectMany(TasksIn).ToList();
            if (!sprints.All(Kept))
            {
                return new InspectionCostStatsDto(tasks.Count, 0, null, TokenLabel.None, null, TokenLabel.None, null, null, TokenLabel.None, null, false);
            }
            var withRuns = tasks.Where(placed => placed.Tokens is not null).ToList();
            long? perTask = withRuns.Count == 0 ? null : (long)Math.Round(withRuns.Average(placed => placed.Tokens!.Total), MidpointRounding.AwayFromZero);
            long? total = withRuns.Count == 0 ? null : withRuns.Sum(placed => placed.Tokens!.Total);
            var mergedPrs = sprints.Sum(sprint => _mergedPrs.GetValueOrDefault(sprint.Id));
            long? perPr = mergedPrs == 0 || total is null ? null : (long)Math.Round((double)total.Value / mergedPrs, MidpointRounding.AwayFromZero);
            return new InspectionCostStatsDto(
                tasks.Count,
                withRuns.Count,
                perTask,
                TokenLabel.Of(perTask),
                total,
                TokenLabel.Of(total),
                mergedPrs,
                perPr,
                TokenLabel.Of(perPr),
                Change(perTask, against),
                true);
        }

        private int? Change(long? current, IReadOnlyList<InspectionSprint>? against)
        {
            if (current is null || against is null || !against.All(Kept))
            {
                return null;
            }
            var previous = against.SelectMany(TasksIn).Where(placed => placed.Tokens is not null).Select(placed => placed.Tokens!.Total).ToList();
            if (previous.Count == 0)
            {
                return null;
            }
            var mean = previous.Average();
            return mean == 0 ? null : (int)Math.Round((current.Value - mean) / mean * 100, MidpointRounding.AwayFromZero);
        }

        private static InspectionCostTaskDto TaskDto(Placed placed)
        {
            var tokens = placed.Tokens!;
            return new InspectionCostTaskDto(
                placed.Fact.Key,
                placed.Fact.Title,
                placed.Fact.FeatureKey,
                placed.Who,
                placed.Fact.AgentName is not null,
                tokens.Models,
                tokens.Runs,
                tokens.Total,
                TokenLabel.Of(tokens.Total),
                DisplayDate.Duration(LeadTimeStats.Round(placed.LeadHours)),
                placed.SprintId);
        }

        private static IReadOnlyList<InspectionCostFeatureDto> ByFeature(IReadOnlyList<Placed> withRuns)
            => [.. withRuns
                .GroupBy(placed => placed.Fact.FeatureKey, StringComparer.Ordinal)
                .Select(group =>
                {
                    var total = group.Sum(placed => placed.Tokens!.Total);
                    return new InspectionCostFeatureDto(
                        group.Key,
                        group.Key is null ? LeadTimeAggregator.NoFeatureLabel : group.First().Fact.FeatureTitle ?? group.Key,
                        group.Count(),
                        total,
                        TokenLabel.Of(total));
                })
                .OrderByDescending(feature => feature.Tokens)
                .ThenBy(feature => feature.Key, StringComparer.Ordinal)];

        // False when the sprint's first day, at the start of that day in the zone, is before the store's KeptSince.
        private bool Kept(InspectionSprint sprint)
            => _tokens.KeptSince is not { } keptSince || DisplayDate.StartOfDay(sprint.StartDate, _zone) >= keptSince;

        private List<Placed> TasksIn(InspectionSprint sprint) => [.. _bySprint[sprint.Id]];
    }
}
