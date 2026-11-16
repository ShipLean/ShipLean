using ShipLean.Application.Features.InspectionMetrics;
using ShipLean.Application.Features.InspectionMetrics.Aggregate;
using ShipLean.Application.Features.InspectionMetrics.Facts;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Application.Features.InspectionMetrics.Quality;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.UnitTest.InspectionMetrics;

// The quality aggregator's own rules beyond the contract tests (QT4). Sprints as QualityAggregatorContractTests: Sprint 1 (7 to 18
// Sep), Sprint 2 (21 Sep to 2 Oct), Sprint 3 (5 to 16 Oct), Sprint 4 (19 to 30 Oct); today is Sat 24 Oct 2026. Weights 8/4/2/1.
[TestFixture]
public class QualityAggregatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 14, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyDictionary<int, int> Weights = new Dictionary<int, int> { [1] = 8, [2] = 4, [3] = 2, [4] = 1 };

    private static readonly InspectionSprint S2 = Sprint(2, new DateOnly(2026, 9, 21));
    private static readonly InspectionSprint S3 = Sprint(3, new DateOnly(2026, 10, 5));
    private static readonly InspectionSprint S4 = Sprint(4, new DateOnly(2026, 10, 19));
    private static readonly InspectionSprint[] All = [Sprint(1, new DateOnly(2026, 9, 7)), S2, S3, S4];

    private static InspectionSprint Sprint(int number, DateOnly start)
        => new(new Guid($"00000000-0000-4000-8000-00000000000{number}"), $"Sprint {number}", start, start.AddDays(11));

    private static DateTimeOffset At(int month, int day, int hour) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private static BugFact Bug(string key, int? severity, DateTimeOffset filed, bool foundBeforeRelease = false, string? causedBy = null)
        => new(key, $"Bug {key}", severity, filed, foundBeforeRelease, causedBy);

    private static InspectionMetricsViewDto Apply(BugFact[] bugs, MergedPrFact[]? prs = null, LeadTimeFacts? causes = null)
    {
        InspectionSprint[] shown = [S2, S3, S4];
        var sprints = new InspectionSprints(new InspectionProjectDto(Guid.NewGuid(), "SHIP", "ShipLean"), All);
        var window = new InspectionWindow(new DateOnly(2026, 7, 1), new DateOnly(2026, 10, 24), shown);
        var selection = new InspectionMetricsSelection(InspectionRange.Last6, InspectionCut.None, null, null);
        var view = new LeadTimeAggregator().Build(sprints, window, LeadTimeFacts.Empty, Array.Empty<TaskLeadTime>(), selection, Now, TimeZoneInfo.Utc);
        var facts = new BugFacts(AnyBugs: true, bugs, prs ?? [], Weights, causes ?? LeadTimeFacts.Empty);
        return new QualityAggregator(new TaskLeadTimeMeasure()).Apply(view, sprints, window, facts, Now, TimeZoneInfo.Utc);
    }

    private static int?[] Escaping(InspectionMetricsViewDto view) => [.. view.Periods.Select(period => period.Quality?.Escaping)];

    // A bug filed in Sprint 4 whose cause's lead time ended (last commit) in Sprint 3 counts in Sprint 3. Mutation: always use filedAt.
    [Test]
    public void Apply_WhenBugHasCausedBy_ShouldCountInCauseSprint()
    {
        var causes = new LeadTimeFactsBuilder()
            .WithTask("SHIP-10", title: "Report date filter")
            .WithStart("SHIP-10", At(10, 6, 9))
            .WithCommit("SHIP-10", "abc1234", At(10, 8, 15))
            .Build();

        var view = Apply([Bug("SHIP-1", 2, At(10, 21, 9), causedBy: "SHIP-10")], causes: causes);

        Escaping(view).ShouldBe([0, 1, 0]);
        view.Bugs.ShouldNotBeNull().Single().PeriodId.ShouldBe(S3.Id);
    }

    // A cause that never ended (no commit time, never resolved) has no sprint: the bug counts where it was filed (design D11
    // "until"). Mutation: drop a bug whose cause has no end.
    [Test]
    public void Apply_WhenCauseHasNoEnd_ShouldCountInFiledSprint()
    {
        var causes = new LeadTimeFactsBuilder()
            .WithTask("SHIP-10", title: "Report date filter")
            .WithStart("SHIP-10", At(10, 6, 9))
            .Build();

        var view = Apply([Bug("SHIP-1", 2, At(10, 21, 9), causedBy: "SHIP-10")], causes: causes);

        Escaping(view).ShouldBe([0, 0, 1]);
    }

    // Found before release bugs are listed and counted as such, never as escaping, weighted or by severity. Mutation: count every bug
    // as escaping.
    [Test]
    public void Apply_WhenFoundBeforeRelease_ShouldNotCountAsEscaping()
    {
        var view = Apply([Bug("SHIP-1", 1, At(10, 6, 9), foundBeforeRelease: true), Bug("SHIP-2", 4, At(10, 7, 9))]);

        var s3 = view.Periods[1].Quality.ShouldNotBeNull();
        (s3.Escaping, s3.Weighted, s3.FoundBeforeRelease).ShouldBe((1, 1, 1));
        s3.EscapingBySeverity.ShouldBe(new InspectionSeverityCountsDto(0, 0, 0, 1, 0));
        view.Bugs.ShouldNotBeNull().Select(bug => bug.Key).ShouldBe(["SHIP-1", "SHIP-2"]);
    }

    // A PR merged on the Saturday after Sprint 3's last day counts in Sprint 3 (the weekend grace). Mutation: place PRs by the day
    // holding them only.
    [Test]
    public void Apply_WhenPrMergedInWeekendGrace_ShouldCountInTheSprintBefore()
    {
        var view = Apply([Bug("SHIP-1", 2, At(10, 6, 9))], [new MergedPrFact("https://github.com/ShipLean/ShipLean/pull/1", At(10, 17, 9))]);

        view.Periods.Select(period => period.Quality?.MergedPrs).ShouldBe([null, 1, null]);
        view.Periods[1].Quality.ShouldNotBeNull().Per100Prs.ShouldBe(100.0);
    }
}
