using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.InspectionMetrics;
using ShipLean.Application.Features.InspectionMetrics.Aggregate;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Application.Features.InspectionMetrics.Quality;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.UnitTest.InspectionMetrics;

// What the real quality aggregator (QT4) must add to the lead-time view (QK3, QK5). The aggregator comes from the feature's own
// service module, so its constructor (QT4 adds ITaskLeadTimeMeasure) is not pinned here. Red until QT4 replaces the stub: the stub
// leaves every Quality null. Sprints as LeadTimeAggregatorContractTests: Sprint 1 (7 to 18 Sep) ends in Q3; Sprints 2 to 4 (21 Sep
// to 2 Oct, 5 to 16 Oct, 19 to 30 Oct) end in Q4. Today is Sat 24 Oct 2026, inside Sprint 4. Default weights 8/4/2/1.
[TestFixture]
public class QualityAggregatorContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 14, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyDictionary<int, int> Weights = new Dictionary<int, int> { [1] = 8, [2] = 4, [3] = 2, [4] = 1 };

    private static readonly InspectionSprint S1 = Sprint(1, new DateOnly(2026, 9, 7));
    private static readonly InspectionSprint S2 = Sprint(2, new DateOnly(2026, 9, 21));
    private static readonly InspectionSprint S3 = Sprint(3, new DateOnly(2026, 10, 5));
    private static readonly InspectionSprint S4 = Sprint(4, new DateOnly(2026, 10, 19));
    private static readonly InspectionSprint[] All = [S1, S2, S3, S4];

    private static InspectionSprint Sprint(int number, DateOnly start)
        => new(new Guid($"00000000-0000-4000-8000-00000000000{number}"), $"Sprint {number}", start, start.AddDays(11));

    private static DateTimeOffset At(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private static BugFact Bug(string key, int? severity, DateTimeOffset filed, bool foundBeforeRelease = false, string? causedBy = null)
        => new(key, $"Bug {key}", severity, filed, foundBeforeRelease, causedBy);

    private static BugFacts Facts(BugFact[] bugs, MergedPrFact[]? prs = null, LeadTimeFacts? causes = null)
        => new(bugs, prs ?? [], Weights, causes ?? LeadTimeFacts.Empty);

    private static MergedPrFact[] Prs(int count, DateTimeOffset mergedAt)
        => [.. Enumerable.Range(1, count).Select(n => new MergedPrFact($"https://github.com/ShipLean/ShipLean/pull/{n}{mergedAt:MMdd}", mergedAt))];

    private static InspectionMetricsViewDto Apply(InspectionSprint[] shown, BugFacts facts, InspectionSprint? period = null)
    {
        var sprints = new InspectionSprints(new InspectionProjectDto(Guid.NewGuid(), "SHIP", "ShipLean"), All);
        var window = new InspectionWindow(new DateOnly(2026, 7, 1), new DateOnly(2026, 10, 24), shown);
        var selection = new InspectionMetricsSelection(InspectionRange.Last6, InspectionCut.None, period, null);
        var view = new LeadTimeAggregator().Build(sprints, window, LeadTimeFacts.Empty, Array.Empty<TaskLeadTime>(), selection, Now, TimeZoneInfo.Utc);
        var services = new ServiceCollection();
        new InspectionMetricsServices().AddServices(services, new ConfigurationBuilder().Build());
        using var provider = (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);
        return provider.GetRequiredService<IQualityAggregator>().Apply(view, sprints, window, facts, Now, TimeZoneInfo.Utc);
    }

    // Mutation: count found before release bugs as escaping.
    [Test]
    public void Apply_WhenBugsFiled_ShouldCountEscapingAndWeightedPerPeriodTurnedGreenByQT4()
    {
        var view = Apply([S3, S4], Facts(
        [
            Bug("SHIP-1", 1, At(10, 6, 9)),
            Bug("SHIP-2", 3, At(10, 8, 9)),
            Bug("SHIP-3", 2, At(10, 9, 9), foundBeforeRelease: true),
            Bug("SHIP-4", null, At(10, 20, 9)),
        ]));

        view.Periods.Select(p => (p.Id, p.Quality?.Escaping, p.Quality?.Weighted, p.Quality?.FoundBeforeRelease))
            .ShouldBe([(S3.Id, (int?)2, (int?)10, (int?)1), (S4.Id, 1, 0, 0)]);
        view.Headline.Finished.ShouldNotBeNull().Quality.ShouldNotBeNull().Escaping.ShouldBe(2);
        view.Quality.ShouldNotBeNull().Escaping.ShouldBe(3);
    }

    // Mutation: divide by the range's merged PRs instead of the sprint's.
    [Test]
    public void Apply_WhenPrsMerged_ShouldGivePer100PrsAndNullWithoutPrsTurnedGreenByQT4()
    {
        var view = Apply([S3, S4], Facts([Bug("SHIP-1", 2, At(10, 6, 9)), Bug("SHIP-2", 2, At(10, 7, 9)), Bug("SHIP-4", 2, At(10, 20, 9))], Prs(8, At(10, 12, 9))));

        var s3 = view.Periods[0].Quality.ShouldNotBeNull();
        (s3.MergedPrs, s3.Per100Prs).ShouldBe(((int?)8, (double?)25.0));
        var s4 = view.Periods[1].Quality.ShouldNotBeNull();
        (s4.MergedPrs, s4.Per100Prs).ShouldBe(((int?)null, (double?)null));
    }

    // Mutation: always count a bug in the sprint it was filed.
    [Test]
    public void Apply_WhenBugHasCausedBy_ShouldCountInTheSprintItsCauseEndedTurnedGreenByQT4()
    {
        var causes = new LeadTimeFactsBuilder()
            .WithTask("SHIP-10", title: "Report date filter")
            .WithStart("SHIP-10", At(9, 22, 9))
            .WithCommit("SHIP-10", "abc1234", At(9, 24, 15))
            .Build();

        var view = Apply([S2, S3, S4], Facts([Bug("SHIP-1", 2, At(10, 20, 9), causedBy: "SHIP-10")], causes: causes));

        view.Periods.Select(p => p.Quality?.Escaping).ShouldBe([1, 0, 0]);
    }

    // Mutation: sum only the shown sprints of the quarter.
    [Test]
    public void Apply_WhenASprintOfTheQuarterIsNotShown_ShouldStillSumItTurnedGreenByQT4()
    {
        var view = Apply([S3, S4], Facts([Bug("SHIP-1", 2, At(9, 23, 9)), Bug("SHIP-2", 2, At(10, 6, 9)), Bug("SHIP-3", 2, At(9, 10, 9))]));

        view.Quarters.Select(q => (q.Quarter, q.Quality?.Escaping)).ShouldBe([("Q4 2026", (int?)2)]);
        view.Headline.LastQuarter.ShouldNotBeNull().Quality.ShouldNotBeNull().Escaping.ShouldBe(1);
    }

    // Mutation: leave found before release bugs out of the list.
    [Test]
    public void Apply_WhenPeriodOpened_ShouldListEveryBugCountedInItTurnedGreenByQT4()
    {
        var causes = new LeadTimeFactsBuilder()
            .WithTask("SHIP-10", title: "Report date filter")
            .WithStart("SHIP-10", At(10, 5, 9))
            .WithCommit("SHIP-10", "abc1234", At(10, 6, 15))
            .Build();

        var view = Apply([S3, S4], Facts(
        [
            Bug("SHIP-2", 3, At(10, 9, 9), foundBeforeRelease: true),
            Bug("SHIP-1", 2, At(10, 7, 9)),
            Bug("SHIP-3", 2, At(10, 20, 9), causedBy: "SHIP-10"),
            Bug("SHIP-4", 2, At(10, 21, 9)),
        ], causes: causes), period: S3);

        var detail = view.PeriodDetail.ShouldNotBeNull();
        detail.Bugs.ShouldNotBeNull().Select(b => (b.Key, b.FoundBeforeRelease, b.CausedByKey, b.CausedByTitle))
            .ShouldBe([("SHIP-1", false, (string?)null, (string?)null), ("SHIP-2", true, null, null), ("SHIP-3", false, "SHIP-10", "Report date filter")]);
        detail.Quality.ShouldNotBeNull().Escaping.ShouldBe(2);
    }

    // The no-bugs rule (QK3): with no bugs every Quality and Bugs stays null, so the page shows "No bugs yet". Green with the stub;
    // it keeps QT4's aggregator from filling zeros. Mutation (for QT4): return zero stats when there are no bugs.
    [Test]
    public void Apply_WhenProjectHasNoBugs_ShouldLeaveEveryQualityNull()
    {
        var view = Apply([S3, S4], Facts([], Prs(3, At(10, 12, 9))), period: S3);

        view.Quality.ShouldBeNull();
        view.Periods.ShouldAllBe(p => p.Quality == null);
        view.Quarters.ShouldAllBe(q => q.Quality == null);
        view.PeriodDetail.ShouldNotBeNull().Bugs.ShouldBeNull();
    }
}
