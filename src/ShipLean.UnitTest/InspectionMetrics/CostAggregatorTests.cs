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

// The real cost aggregator (CT3, CK2, CK3), fed totals built by hand. Sprint 3 is 5 to 16 Oct and Sprint 4 19 to 30 Oct 2026, Sprint
// 2 is 21 Sep to 2 Oct (not shown, in Q4) and Sprint 1 7 to 18 Sep (Q3); today is Sat 24 Oct, inside Sprint 4.
[TestFixture]
public class CostAggregatorTests
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

    private sealed record TaskSpec(string Key, int Month, int Day, string? FeatureKey = "SHIP-99", string? FeatureTitle = "Coupons v2");

    // Each task started at 9:00 on its day and resolved a day later, so it ends in the sprint holding that next day.
    private static LeadTimeFacts Facts(params TaskSpec[] tasks)
    {
        var builder = new LeadTimeFactsBuilder();
        foreach (var task in tasks)
        {
            builder.WithTask(task.Key, featureKey: task.FeatureKey, featureTitle: task.FeatureTitle, agentName: "Claude Code")
                .WithStart(task.Key, At(task.Month, task.Day, 9))
                .WithResolved(task.Key, At(task.Month, task.Day + 1, 9));
        }
        return builder.Build();
    }

    private static LeadTimeFacts Facts(params (string Key, int Month, int Day)[] tasks) => Facts([.. tasks.Select(t => new TaskSpec(t.Key, t.Month, t.Day))]);

    private static TaskTokenTotals Tokens(DateTimeOffset? keptSince, params (string Key, long In, long Out)[] items)
        => new(true, null, keptSince, items.ToDictionary(i => i.Key, i => new TaskTokens(2, i.In, i.Out, ["claude-sonnet-5-5"]), StringComparer.Ordinal));

    private static TaskTokenTotals Tokens(params (string Key, long Total)[] items) => Tokens(null, [.. items.Select(i => (i.Key, i.Total, 0L))]);

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
    public void Build_WhenTaskHasNoRuns_ShouldLeaveItOutOfMean()
    {
        var cost = Build(Facts(("SHIP-1", 10, 6), ("SHIP-2", 10, 7)), Tokens(("SHIP-1", 1000)));

        var stats = PeriodStats(cost, S3);
        (stats.Tasks, stats.TasksWithRuns, stats.TokensPerTask, stats.TokensTotal).ShouldBe((2, 1, (long?)1000, (long?)1000));
        stats.TokensPerTaskLabel.ShouldBe("1.0k");
    }

    // Mutation: divide by 1 when no PR merged.
    [Test]
    public void Build_WhenNoMergedPrs_ShouldReturnNullPerPr()
    {
        var cost = Build(Facts(("SHIP-1", 10, 6), ("SHIP-4", 10, 20)), Tokens(("SHIP-1", 4000), ("SHIP-4", 1000)), new Dictionary<Guid, int> { [S3.Id] = 2 });

        PeriodStats(cost, S3).TokensPerMergedPr.ShouldBe(2000);
        var s4 = PeriodStats(cost, S4);
        (s4.MergedPrs, s4.TokensPerMergedPr, s4.TokensPerMergedPrLabel).ShouldBe(((int?)0, (long?)null, "—"));
    }

    // Mutation: ignore KeptSince.
    [Test]
    public void Build_WhenPeriodBeforeKeptSince_ShouldNotBeKept()
    {
        var cost = Build(Facts(("SHIP-1", 10, 6), ("SHIP-4", 10, 20)), Tokens(At(10, 5, 12), ("SHIP-1", 800, 200), ("SHIP-4", 500, 500)));

        var s3 = PeriodStats(cost, S3);
        (s3.Kept, s3.TokensPerTask, s3.TokensTotal, s3.TokensTotalLabel).ShouldBe((false, (long?)null, (long?)null, "—"));
        PeriodStats(cost, S4).Kept.ShouldBeTrue();
        cost.Range!.Kept.ShouldBeFalse();
        cost.MostTokens.Select(t => t.Key).ShouldBe(["SHIP-4"]);
    }

    // Mutation: default the numbers to 0.
    [Test]
    public void Build_WhenTokensUnavailable_ShouldReturnNullsNotZero()
    {
        var cost = Build(Facts(("SHIP-1", 10, 6)), TaskTokenTotals.Unavailable("Needs the ClickHouse store."));

        cost.Available.ShouldBeFalse();
        cost.Note.ShouldBe("Needs the ClickHouse store.");
        (cost.Range, cost.Headline.Finished, cost.Headline.SoFar).ShouldBe((null, null, null));
        (cost.Periods.Count + cost.Quarters.Count + cost.MostTokens.Count + cost.ByFeature.Count).ShouldBe(0);
    }

    // Mutation: sum only the shown sprints into the quarter.
    [Test]
    public void Build_ShouldSumQuarterOverSprintsNotShown()
    {
        var cost = Build(
            Facts(("SHIP-1", 10, 6), ("SHIP-5", 9, 22), ("SHIP-6", 9, 8)),
            Tokens(("SHIP-1", 1000), ("SHIP-5", 3000), ("SHIP-6", 1000)));

        var quarter = cost.Quarters.ShouldHaveSingleItem();
        quarter.Quarter.ShouldBe("Q4 2026");
        (quarter.Stats.TasksWithRuns, quarter.Stats.TokensPerTask, quarter.Stats.TokensTotal).ShouldBe((2, (long?)2000, (long?)4000));
        quarter.Stats.ChangePctVsPrevious.ShouldBeNull();
        cost.Headline.QuarterToDate!.TokensTotal.ShouldBe(4000);
        cost.Headline.LastQuarter!.TokensTotal.ShouldBe(1000);
    }

    // Mutation: compare with the previous shown sprint, or flip the sign.
    [Test]
    public void Build_ShouldCompareASprintWithThePreviousOne()
    {
        var cost = Build(Facts(("SHIP-1", 10, 6), ("SHIP-4", 10, 20)), Tokens(("SHIP-1", 1000), ("SHIP-4", 1500)));

        PeriodStats(cost, S3).ChangePctVsPrevious.ShouldBeNull();
        PeriodStats(cost, S4).ChangePctVsPrevious.ShouldBe(50);
        cost.Headline.SoFar!.ChangePctVsPrevious.ShouldBe(50);
        cost.Headline.Finished!.TokensTotal.ShouldBe(1000);
    }

    // Mutation: take 7 or 9, or sort ties in reverse.
    [Test]
    public void Build_ShouldListTheEightTasksWithMostTokens()
    {
        var specs = Enumerable.Range(1, 10).Select(i => new TaskSpec($"SHIP-{i}", 10, 5 + i - 1)).ToArray();
        var tokens = specs.Select(s => (s.Key, Total: s.Key == "SHIP-10" ? 5000L : 1000L * int.Parse(s.Key[5..], System.Globalization.CultureInfo.InvariantCulture))).ToArray();

        var cost = Build(Facts(specs), Tokens(tokens));

        var top = cost.MostTokens;
        top.Count.ShouldBe(8);
        top.Select(t => t.Key).ShouldBe(["SHIP-9", "SHIP-8", "SHIP-7", "SHIP-6", "SHIP-10", "SHIP-5", "SHIP-4", "SHIP-3"]);
        (top[0].TokensLabel, top[0].Agent, top[0].Who, top[0].Runs, top[0].LeadLabel).ShouldBe(("9.0k", true, "Claude Code", 2, "1 day"));
    }

    // Mutation: drop the Feature-less tasks, or sort ascending.
    [Test]
    public void Build_ShouldTotalTokensByFeature()
    {
        var specs = new[]
        {
            new TaskSpec("SHIP-1", 10, 6),
            new TaskSpec("SHIP-2", 10, 7, null, null),
            new TaskSpec("SHIP-3", 10, 8, null, null),
            new TaskSpec("SHIP-4", 10, 9, "SHIP-77", "Search"),
        };

        var cost = Build(Facts(specs), Tokens(("SHIP-1", 1000), ("SHIP-2", 700), ("SHIP-3", 800), ("SHIP-4", 500)));

        cost.ByFeature.Select(f => (f.Key, f.Title, f.Tasks, f.Tokens)).ShouldBe(
            [((string?)null, "No Feature", 2, 1500L), ("SHIP-99", "Coupons v2", 1, 1000L), ("SHIP-77", "Search", 1, 500L)]);
    }

    // Mutation: sum only one of the shown sprints, or divide by the tasks with runs.
    [Test]
    public void Build_ShouldTotalTheRangeOverTheShownSprints()
    {
        var cost = Build(Facts(("SHIP-1", 10, 6), ("SHIP-2", 10, 7), ("SHIP-4", 10, 20), ("SHIP-6", 9, 8)), Tokens(("SHIP-1", 1000), ("SHIP-4", 3000), ("SHIP-6", 9000)));

        var range = cost.Range!;
        (range.Tasks, range.TasksWithRuns, range.TokensPerTask, range.TokensTotal, range.ChangePctVsPrevious).ShouldBe((3, 2, (long?)2000, (long?)4000, (int?)null));
        range.TasksWithRunsPct.ShouldBe(67);
    }

    // Mutation: round down, or give a percent when the span is not kept.
    [Test]
    public void Build_ShouldGiveThePercentOfTasksWithRuns()
    {
        var cost = Build(Facts(("SHIP-1", 10, 6), ("SHIP-2", 10, 7), ("SHIP-3", 10, 8), ("SHIP-4", 10, 20)), Tokens(At(10, 10, 0), ("SHIP-1", 1000, 0), ("SHIP-4", 500, 0)));

        PeriodStats(cost, S3).TasksWithRunsPct.ShouldBeNull();
        var s4 = PeriodStats(cost, S4);
        (s4.Tasks, s4.TasksWithRuns, s4.TasksWithRunsPct).ShouldBe((1, 1, (int?)100));
    }

    // Mutation: point the headline at another period, or at the quarter's shown sprints only.
    [Test]
    public void Build_ShouldMirrorTheLeadTimeHeadline()
    {
        var cost = Build(Facts(("SHIP-1", 10, 6), ("SHIP-4", 10, 20), ("SHIP-5", 9, 22)), Tokens(("SHIP-1", 1000), ("SHIP-4", 1500), ("SHIP-5", 3000)));

        cost.Headline.Finished!.TokensTotal.ShouldBe(1000);
        cost.Headline.SoFar!.TokensTotal.ShouldBe(1500);
        cost.Headline.QuarterToDate!.TokensTotal.ShouldBe(5500);
        cost.Headline.QuarterToDate.ChangePctVsPrevious.ShouldBeNull();
        cost.Headline.LastQuarter!.TokensTotal.ShouldBeNull();
    }

    [TestCase(950L, "950")]
    [TestCase(18_150L, "18.2k")]
    [TestCase(999_990L, "1.0M")]
    [TestCase(1_234_567L, "1.2M")]
    public void Of_ShouldShortenTokens(long tokens, string label)
    {
        TokenLabel.Of(tokens).ShouldBe(label);
    }

    [Test]
    public void Of_WhenNoNumber_ShouldReadDash()
    {
        TokenLabel.Of(null).ShouldBe("—");
    }
}
