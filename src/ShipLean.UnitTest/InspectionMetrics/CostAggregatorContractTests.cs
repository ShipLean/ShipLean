using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.InspectionMetrics;
using ShipLean.Application.Features.InspectionMetrics.Aggregate;
using ShipLean.Application.Features.InspectionMetrics.Cost;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Inspection;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.UnitTest.InspectionMetrics;

// What the real cost aggregator (CT3) must build (CK2, CK3). The aggregator and the measure come from the feature's own service
// module, so their constructors are not pinned here. Red until CT3 replaces the stub: the stub reports the cost unavailable. Sprints
// as QualityAggregatorContractTests: Sprint 3 is 5 to 16 Oct and Sprint 4 19 to 30 Oct 2026; today is Sat 24 Oct, inside Sprint 4.
[TestFixture]
public class CostAggregatorContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 14, 0, 0, TimeSpan.Zero);

    private static readonly InspectionSprint S1 = Sprint(1, new DateOnly(2026, 9, 7));
    private static readonly InspectionSprint S2 = Sprint(2, new DateOnly(2026, 9, 21));
    private static readonly InspectionSprint S3 = Sprint(3, new DateOnly(2026, 10, 5));
    private static readonly InspectionSprint S4 = Sprint(4, new DateOnly(2026, 10, 19));
    private static readonly InspectionSprint[] All = [S1, S2, S3, S4];

    private static InspectionSprint Sprint(int number, DateOnly start)
        => new(new Guid($"00000000-0000-4000-8000-00000000000{number}"), $"Sprint {number}", start, start.AddDays(11));

    private static DateTimeOffset At(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    // Each task started at 9:00 on its day and resolved a day later, so it ends in the sprint holding that next day.
    private static LeadTimeFacts Tasks(params (string Key, int Month, int Day)[] tasks)
    {
        var builder = new LeadTimeFactsBuilder();
        foreach (var (key, month, day) in tasks)
        {
            builder.WithTask(key, agentName: "Claude Code").WithStart(key, At(month, day, 9)).WithResolved(key, At(month, day + 1, 9));
        }
        return builder.Build();
    }

    private static TaskTokenTotals Tokens(DateTimeOffset? keptSince, params (string Key, long In, long Out)[] items)
        => new(true, null, keptSince, items.ToDictionary(i => i.Key, i => new TaskTokens(1, i.In, i.Out, ["claude-sonnet-5-5"]), StringComparer.Ordinal));

    private static InspectionCostDto Build(LeadTimeFacts facts, TaskTokenTotals tokens, IReadOnlyDictionary<Guid, int>? mergedPrs = null)
    {
        var services = new ServiceCollection();
        new InspectionMetricsServices().AddServices(services, new ConfigurationBuilder().Build());
        using var provider = (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var measure = provider.GetRequiredService<ITaskLeadTimeMeasure>();
        var measured = facts.Tasks.Select(task => measure.Measure(task, facts)).OfType<TaskLeadTime>().ToList();
        var sprints = new InspectionSprints(new InspectionProjectDto(Guid.NewGuid(), "SHIP", "ShipLean"), All);
        var window = new InspectionWindow(new DateOnly(2026, 7, 1), new DateOnly(2026, 10, 24), [S3, S4]);
        var selection = new InspectionMetricsSelection(InspectionRange.Last6, InspectionCut.None, null, null);
        var view = new LeadTimeAggregator().Build(sprints, window, facts, measured, selection, Now, TimeZoneInfo.Utc);
        return provider.GetRequiredService<ICostAggregator>()
            .Build(view, sprints, window, facts, measured, tokens, mergedPrs ?? new Dictionary<Guid, int>(), Now, TimeZoneInfo.Utc);
    }

    private static InspectionCostStatsDto PeriodStats(InspectionCostDto cost, InspectionSprint sprint)
        => cost.Periods.Where(p => p.PeriodId == sprint.Id).ShouldHaveSingleItem().Stats;

    // Mutation: count a task with no runs as zero tokens in the mean.
    [Test]
    public void Build_WhenATaskHasNoRuns_ShouldLeaveItOutOfTheMean()
    {
        var cost = Build(Tasks(("SHIP-1", 10, 6), ("SHIP-2", 10, 7)), Tokens(null, ("SHIP-1", 800, 200)));

        cost.Available.ShouldBeTrue();
        var stats = PeriodStats(cost, S3);
        (stats.Tasks, stats.TasksWithRuns, stats.TokensPerTask, stats.TokensTotal).ShouldBe((2, 1, (long?)1000, (long?)1000));
        stats.Kept.ShouldBeTrue();
    }

    // Mutation: divide by the range's merged PRs instead of the sprint's; give 0 instead of null without PRs.
    [Test]
    public void Build_WhenNoPrMerged_ShouldGiveNullPerMergedPr()
    {
        var cost = Build(
            Tasks(("SHIP-1", 10, 6), ("SHIP-4", 10, 20)),
            Tokens(null, ("SHIP-1", 3000, 1000), ("SHIP-4", 500, 500)),
            new Dictionary<Guid, int> { [S3.Id] = 2 });

        var s3 = PeriodStats(cost, S3);
        (s3.MergedPrs, s3.TokensPerMergedPr).ShouldBe(((int?)2, (long?)2000));
        var s4 = PeriodStats(cost, S4);
        s4.TokensPerMergedPr.ShouldBeNull();
        s4.TokensTotal.ShouldBe(1000);
    }

    // Mutation: compare KeptSince with the sprint's end instead of its start.
    [Test]
    public void Build_WhenAPeriodStartedBeforeKeptSince_ShouldMarkItNotKept()
    {
        var cost = Build(Tasks(("SHIP-1", 10, 6), ("SHIP-4", 10, 20)), Tokens(At(10, 10, 0), ("SHIP-1", 800, 200), ("SHIP-4", 500, 500)));

        var s3 = PeriodStats(cost, S3);
        s3.Kept.ShouldBeFalse();
        s3.TokensPerTask.ShouldBeNull();
        s3.TokensPerTaskLabel.ShouldBe("—");
        var s4 = PeriodStats(cost, S4);
        s4.Kept.ShouldBeTrue();
        s4.TokensPerTask.ShouldBe(1000);
    }

    // Mutation: drop the store's problem from the note.
    [Test]
    public void Build_WhenTokensUnavailable_ShouldCarryTheProblem()
    {
        var cost = Build(Tasks(("SHIP-1", 10, 6)), TaskTokenTotals.Unavailable("Tokens per task needs the ClickHouse store."));

        cost.Available.ShouldBeFalse();
        cost.Note.ShouldBe("Tokens per task needs the ClickHouse store.");
        cost.Periods.ShouldBeEmpty();
        cost.Range.ShouldBeNull();
    }
}
