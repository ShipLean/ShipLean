using System.Globalization;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.Core;

namespace ShipLean.Application.Features.InspectionMetrics.Aggregate;

// The view (K2, K7), following the design's INS.build. A task falls in the sprint its lead time ended in, by the end's day in the
// viewer's zone; a task ending on the two days after a sprint's last day (its weekend) still counts in it. Only started sprints
// are periods. The headline is the last finished sprint with the running one so far (design D6); quarters are calendar quarters
// by sprint end date and sum every started sprint of the quarter, shown or not (design D7). Those period rules live in
// Periods/InspectionPeriods, which quality shares. Every Quality and Bugs is left null here: the quality aggregator sets them.
public sealed class LeadTimeAggregator : ILeadTimeAggregator
{
    public const string Caveat = "These numbers tell you where to look, not what caused a change.";
    public const string PeopleKey = "people";
    public const string PeopleLabel = TaskPlacement.PeopleLabel;
    public const string NoFeatureKey = "none";
    public const string NoFeatureLabel = "No Feature";

    private const int LongestCount = 6;
    private const double OverTwoDaysHours = 48;

    public InspectionMetricsViewDto Build(
        InspectionSprints sprints,
        InspectionWindow window,
        LeadTimeFacts facts,
        IReadOnlyList<TaskLeadTime> tasks,
        InspectionMetricsSelection selection,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        var run = new Run(sprints, window, facts, tasks, now, zone);
        return new InspectionMetricsViewDto(
            sprints.Project,
            InspectionCadence.Sprints,
            selection.Range,
            selection.Cut,
            InspectionAggregate.Mean,
            now,
            Caveat,
            run.Coverage(),
            run.Headline(),
            run.Periods(),
            run.Quarters(),
            selection.Cut == InspectionCut.None ? null : run.Cuts(selection.Cut),
            [.. run.InRange.OrderByDescending(task => task.LeadHours).ThenBy(task => task.Key, StringComparer.Ordinal).Take(LongestCount).Select(run.TaskDto)],
            run.LeftOut(),
            selection.Period is null ? null : run.PeriodDetail(selection.Period, selection.Select),
            Quality: null,
            Bugs: null,
            Cost: null);
    }

    private sealed class Run
    {
        private readonly InspectionWindow _window;
        private readonly LeadTimeFacts _facts;
        private readonly IReadOnlyList<TaskLeadTime> _tasks;
        private readonly DateTimeOffset _now;
        private readonly TimeZoneInfo _zone;
        private readonly DateOnly _today;
        private readonly IReadOnlyList<InspectionSprint> _started;
        private readonly IReadOnlyDictionary<string, LeadTimeTaskFact> _taskFacts;
        private readonly IReadOnlyList<AggregatedTask> _measured;
        private readonly ILookup<Guid, AggregatedTask> _bySprint;

        public Run(InspectionSprints sprints, InspectionWindow window, LeadTimeFacts facts, IReadOnlyList<TaskLeadTime> tasks, DateTimeOffset now, TimeZoneInfo zone)
        {
            _window = window;
            _facts = facts;
            _tasks = tasks;
            _now = now;
            _zone = zone;
            _today = DisplayDate.Today(now, zone);
            _started = InspectionPeriods.Started(sprints.Sprints, _today);
            _taskFacts = facts.Tasks.ToDictionary(task => task.Key, StringComparer.Ordinal);

            var reviews = facts.Reviews.ToLookup(review => review.ItemKey, StringComparer.Ordinal);
            var commits = facts.Commits.ToLookup(commit => commit.ItemKey, StringComparer.Ordinal);
            var starters = TaskPlacement.Starters(facts);
            _measured =
            [
                .. tasks.Where(task => task.Start is not null).Select(task =>
                {
                    var fact = _taskFacts[task.TaskKey];
                    return new AggregatedTask(
                        task,
                        fact,
                        InspectionPeriods.SprintOf(_started, task.End, zone),
                        TaskPlacement.Who(fact, starters.GetValueOrDefault(fact.Key), facts.People),
                        reviews[fact.Key].Count(),
                        commits[fact.Key].Count());
                }),
            ];
            _bySprint = _measured.Where(task => task.Sprint is not null).ToLookup(task => task.Sprint!.Id);
            var shown = window.Shown.Select(sprint => sprint.Id).ToHashSet();
            InRange = [.. _measured.Where(task => task.Sprint is not null && shown.Contains(task.Sprint.Id))];
        }

        public IReadOnlyList<AggregatedTask> InRange { get; }

        public InspectionCoverageDto Coverage()
        {
            var withCommitTime = InRange.Count(task => task.Lead.EndedBy == LeadTimeEndedBy.LastCommit);
            int? percent = InRange.Count == 0 ? null : (int)Math.Floor(100.0 * withCommitTime / InRange.Count);
            return new InspectionCoverageDto(InRange.Count, withCommitTime, percent);
        }

        public InspectionHeadlineDto Headline()
        {
            var finished = _started.LastOrDefault(sprint => sprint.EndDate < _today);
            var soFar = _started.LastOrDefault(sprint => sprint.EndDate >= _today);
            var thisQuarter = InspectionPeriods.QuarterStart(_today);
            return new InspectionHeadlineDto(
                finished is null ? null : HeadlinePeriod(finished),
                soFar is null ? null : HeadlinePeriod(soFar),
                Quarter(thisQuarter),
                Quarter(thisQuarter.AddMonths(-3)));
        }

        public IReadOnlyList<InspectionPeriodDto> Periods()
            => [.. _window.Shown.Select(sprint =>
            {
                var tasks = TasksIn(sprint);
                var longest = tasks.OrderByDescending(task => task.LeadHours).ThenBy(task => task.Key, StringComparer.Ordinal).FirstOrDefault();
                return new InspectionPeriodDto(
                    sprint.Id,
                    sprint.Name,
                    ShortName(sprint),
                    sprint.StartDate,
                    sprint.EndDate,
                    SprintLabels.Dates(sprint.StartDate, sprint.EndDate, _today),
                    InspectionPeriods.QuarterLabel(InspectionPeriods.QuarterOf(sprint)),
                    IsRunning(sprint),
                    PeriodStats(sprint),
                    [.. tasks.OrderBy(task => task.Lead.End).Select(task => Dot(task, ReferenceEquals(task, longest)))],
                    Quality: null);
            })];

        public IReadOnlyList<InspectionQuarterDto> Quarters()
            => [.. _window.Shown.Select(InspectionPeriods.QuarterOf).Distinct().Select(quarter => Quarter(quarter)!)];

        public IReadOnlyList<InspectionCutRowDto> Cuts(InspectionCut cut)
        {
            Func<AggregatedTask, string> keyOf = cut == InspectionCut.Feature
                ? task => task.Fact.FeatureKey ?? NoFeatureKey
                : task => task.Fact.AgentName ?? PeopleKey;
            return
            [
                .. InRange.GroupBy(keyOf, StringComparer.Ordinal).Select(group =>
                    {
                        var first = group.First();
                        var label = cut == InspectionCut.Feature
                            ? first.Fact.FeatureTitle ?? NoFeatureLabel
                            : first.Fact.AgentName ?? PeopleLabel;
                        var cells = _window.Shown.Select(sprint => PeriodStats(sprint, task => keyOf(task) == group.Key)).ToList();
                        return new InspectionCutRowDto(group.Key, label, cut == InspectionCut.Who && first.Fact.AgentName is not null, cells, LeadTimeStats.Of([.. group]));
                    })
                    .OrderByDescending(row => row.All.Tasks)
                    .ThenBy(row => row.Label, StringComparer.Ordinal),
            ];
        }

        public IReadOnlyList<InspectionLeftOutDto> LeftOut()
        {
            var shown = _window.Shown.Select(sprint => sprint.Id).ToHashSet();
            return
            [
                .. _tasks
                    .Where(task => task.Start is null && InspectionPeriods.SprintOf(_started, task.End, _zone) is { } sprint && shown.Contains(sprint.Id))
                    .OrderBy(task => task.End)
                    .Select(task =>
                    {
                        var fact = _taskFacts[task.TaskKey];
                        var reason = task.LeftOutReason ?? throw new InvalidOperationException($"{task.TaskKey} has no start and no reason it was left out.");
                        return new InspectionLeftOutDto(fact.Key, fact.Title, fact.FeatureKey, reason, task.End, DisplayDate.DayTime(task.End, _zone));
                    }),
            ];
        }

        public InspectionPeriodDetailDto PeriodDetail(InspectionSprint period, string? select)
        {
            var tasks = TasksIn(period);
            var previous = InspectionPeriods.Previous(_started, period);
            var next = _started.FirstOrDefault(sprint => sprint.StartDate > period.StartDate && sprint.Id != period.Id);
            var slowest = Slowest(tasks);
            var selected = select is null ? slowest.FirstOrDefault() : tasks.FirstOrDefault(task => string.Equals(task.Key, select, StringComparison.Ordinal));
            return new InspectionPeriodDetailDto(
                period.Id,
                period.Name,
                SprintLabels.Dates(period.StartDate, period.EndDate, _today),
                InspectionPeriods.QuarterLabel(InspectionPeriods.QuarterOf(period)),
                previous is null ? null : new InspectionPeriodRefDto(previous.Id, ShortName(previous)),
                next is null ? null : new InspectionPeriodRefDto(next.Id, ShortName(next)),
                IsRunning(period),
                PeriodStats(period),
                [.. slowest.Select(TaskDto)],
                Features(period, tasks),
                selected is null ? null : Measurement(selected),
                Quality: null,
                Bugs: null);
        }

        public InspectionTaskDto TaskDto(AggregatedTask task)
        {
            var lead = LeadTimeStats.Round(task.LeadHours);
            var blocked = LeadTimeStats.Round(task.Lead.BlockedHours);
            var rework = LeadTimeStats.Round(task.Lead.ReworkHours);
            return new InspectionTaskDto(
                task.Key,
                task.Fact.Title,
                task.Fact.ParentKey,
                task.Fact.ParentType,
                task.Fact.FeatureKey,
                task.Fact.FeatureTitle,
                task.Who,
                task.Fact.AgentName is not null,
                task.Start,
                DisplayDate.DayTime(task.Start, _zone),
                task.Lead.End,
                DisplayDate.DayTime(task.Lead.End, _zone),
                task.Lead.EndedBy,
                lead,
                DisplayDate.Duration(lead),
                LeadTimeStats.Round(lead - blocked - rework),
                blocked,
                rework,
                LeadTimeStats.Round(task.Lead.UnresolvedTailHours),
                task.Reviews,
                task.Commits,
                task.Sprint?.Id);
        }

        private IReadOnlyList<InspectionPeriodFeatureDto> Features(InspectionSprint period, IReadOnlyList<AggregatedTask> tasks)
            => [.. tasks.Where(task => task.Fact.FeatureKey is not null)
                .GroupBy(task => task.Fact.FeatureKey!, StringComparer.Ordinal)
                .Select(group =>
                {
                    var ended = group.ToList();
                    var all = _measured.Where(task => task.Fact.FeatureKey == group.Key).ToList();
                    var total = _facts.Tasks.Count(task => task.FeatureKey == group.Key);
                    var longest = Slowest(ended)[0];
                    return new InspectionPeriodFeatureDto(
                        group.Key,
                        ended[0].Fact.FeatureTitle ?? group.Key,
                        ended.Count,
                        all.Count(task => DisplayDate.DayOf(task.Lead.End, _zone) < period.StartDate),
                        total,
                        LeadTimeStats.Of(ended),
                        longest.Key,
                        DisplayDate.Duration(LeadTimeStats.Round(longest.LeadHours)),
                        ExecutionLabel(group.Key, all, total));
                })
                .OrderByDescending(feature => feature.TasksEnded)
                .ThenBy(feature => feature.Key, StringComparer.Ordinal)];

        // How long the Feature has been executing: from the first start of its measured tasks to now, or to the last end once every
        // task of it (left out ones included) has ended. The facts carry every live task of the Feature, open ones too.
        private string ExecutionLabel(string featureKey, IReadOnlyList<AggregatedTask> all, int total)
        {
            var first = all.Min(task => task.Start);
            var ended = _tasks.Where(task => _taskFacts[task.TaskKey].FeatureKey == featureKey).ToList();
            var done = ended.Count == total;
            var until = done ? ended.Max(task => task.End) : _now;
            var days = (int)Math.Floor((until - first).TotalDays);
            var text = days == 1 ? "1 day" : $"{days.ToString(CultureInfo.InvariantCulture)} days";
            return done ? text : $"{text} so far";
        }

        private InspectionTaskMeasurementDto Measurement(AggregatedTask task)
            => new(
                TaskDto(task),
                [.. task.Lead.Segments.OrderBy(segment => segment.From).Select(segment =>
                {
                    var hours = LeadTimeStats.Round((segment.To - segment.From).TotalHours);
                    return new InspectionSegmentDto(
                        segment.Kind,
                        segment.From,
                        segment.To,
                        LeadTimeStats.Round((segment.From - task.Start).TotalHours),
                        hours,
                        DisplayDate.Duration(hours),
                        segment.Kind != InspectionSegmentKind.UnresolvedTail);
                })],
                [.. task.Lead.Events.OrderBy(item => item.At).Select(item => new InspectionEventDto(
                    item.Kind,
                    item.At,
                    DisplayDate.DayTime(item.At, _zone),
                    LeadTimeStats.Round((item.At - task.Start).TotalHours),
                    item.Text))]);

        private InspectionHeadlinePeriodDto HeadlinePeriod(InspectionSprint sprint) => new(sprint.Id, sprint.Name, PeriodStats(sprint), Quality: null);

        // A quarter's roll-up over every started sprint ending in it; null when none does. The change is against last quarter: its
        // whole for a finished quarter, and up to the same day into it for the quarter to date.
        private InspectionQuarterDto? Quarter(DateOnly quarterStart)
        {
            var sprints = SprintsEndingIn(quarterStart);
            if (sprints.Count == 0)
            {
                return null;
            }
            var toDate = InspectionPeriods.QuarterStart(_today) == quarterStart;
            var lastQuarter = quarterStart.AddMonths(-3);
            var lastQuarterUntil = lastQuarter.AddDays(_today.DayNumber - quarterStart.DayNumber);
            var previous = SprintsEndingIn(lastQuarter).SelectMany(TasksIn).Where(task => !toDate || DisplayDate.DayOf(task.Lead.End, _zone) <= lastQuarterUntil).ToList();
            return new InspectionQuarterDto(
                InspectionPeriods.QuarterLabel(quarterStart),
                toDate,
                [.. sprints.Select(sprint => sprint.Id)],
                LeadTimeStats.Of([.. sprints.SelectMany(TasksIn)], previous),
                Quality: null);
        }

        private IReadOnlyList<InspectionSprint> SprintsEndingIn(DateOnly quarterStart) => InspectionPeriods.SprintsEndingIn(_started, quarterStart);

        // A sprint's numbers, with the change against the started sprint before it, both over the tasks that pass the filter.
        private LeadTimeStatsDto PeriodStats(InspectionSprint sprint, Func<AggregatedTask, bool>? filter = null)
        {
            var previous = InspectionPeriods.Previous(_started, sprint);
            filter ??= _ => true;
            var before = previous is null ? null : TasksIn(previous).Where(filter).ToList();
            return LeadTimeStats.Of([.. TasksIn(sprint).Where(filter)], before);
        }

        private List<AggregatedTask> TasksIn(InspectionSprint sprint) => [.. _bySprint[sprint.Id]];

        private bool IsRunning(InspectionSprint sprint) => sprint.StartDate <= _today && _today <= sprint.EndDate;

        private static List<AggregatedTask> Slowest(IEnumerable<AggregatedTask> tasks)
            => [.. tasks.OrderByDescending(task => task.LeadHours).ThenBy(task => task.Key, StringComparer.Ordinal)];

        private static InspectionDotDto Dot(AggregatedTask task, bool longest)
        {
            var lead = LeadTimeStats.Round(task.LeadHours);
            return new InspectionDotDto(task.Key, task.Fact.Title, lead, DisplayDate.Duration(lead), task.Lead.EndedBy, lead > OverTwoDaysHours, longest);
        }

        // "Sprint 23" reads "S23"; any other name stays as it is.
        private static string ShortName(InspectionSprint sprint)
            => sprint.Name.StartsWith("Sprint ", StringComparison.Ordinal) ? $"S{sprint.Name["Sprint ".Length..]}" : sprint.Name;
    }
}
