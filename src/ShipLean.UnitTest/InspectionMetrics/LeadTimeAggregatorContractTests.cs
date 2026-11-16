using ShipLean.Application.Features.InspectionMetrics;
using ShipLean.Application.Features.InspectionMetrics.Aggregate;
using ShipLean.Application.Features.InspectionMetrics.Measure;
using ShipLean.Application.Features.InspectionMetrics.Periods;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.UnitTest.InspectionMetrics;

// What the real aggregator (B3) must do with measured tasks (K7). The TaskLeadTime values are built by hand. Red until B3 replaces
// the stub: each test fails because the stub builds no periods. Four two-week sprints: Sprint 1 (Mon 7 Sep to Fri 18 Sep) ends in
// Q3; Sprints 2 to 4 (21 Sep to 2 Oct, 5 to 16 Oct, 19 to 30 Oct) end in Q4. Today is Sat 24 Oct 2026, inside Sprint 4.
[TestFixture]
public class LeadTimeAggregatorContractTests
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

    // A task that took the hours and ended at the time: all active, ended at its last commit.
    private static TaskLeadTime Task(string key, DateTimeOffset end, double hours)
        => new(key, end.AddHours(-hours), end, LeadTimeEndedBy.LastCommit, hours, 0, 0, 0, [], [], null);

    private static InspectionMetricsViewDto Build(InspectionSprint[] shown, params TaskLeadTime[] tasks)
    {
        var facts = tasks.Aggregate(new LeadTimeFactsBuilder(), (builder, task) => builder.WithTask(task.TaskKey)).Build();
        var sprints = new InspectionSprints(new InspectionProjectDto(Guid.NewGuid(), "SHIP", "ShipLean"), All);
        var window = new InspectionWindow(new DateOnly(2026, 9, 7), new DateOnly(2026, 10, 24), shown);
        var selection = new InspectionMetricsSelection(InspectionRange.Last6, InspectionCut.None, null, null);
        return new LeadTimeAggregator().Build(sprints, window, facts, tasks, selection, Now, TimeZoneInfo.Utc);
    }

    // Mutation: use the median instead of the mean.
    [Test]
    public void Periods_WhenTasksEndInASprint_ShouldShowTheirMean_TurnedGreenByB3()
    {
        var view = Build([S3, S4], Task("SHIP-1", At(10, 8, 12), 10), Task("SHIP-2", At(10, 9, 12), 20), Task("SHIP-3", At(10, 14, 12), 60), Task("SHIP-4", At(10, 21, 12), 5));

        view.Periods.Select(p => (p.Id, p.LeadTime.Tasks, p.LeadTime.MeanHours)).ShouldBe([(S3.Id, 3, 30d), (S4.Id, 1, 5d)]);
        view.Periods.Select(p => p.Dots.Count).ShouldBe([3, 1]);
    }

    // Mutation: headline the running sprint instead of the last finished one.
    [Test]
    public void Headline_WhenASprintIsRunning_ShouldBeTheLastFinishedWithTheRunningOneSoFar_TurnedGreenByB3()
    {
        var view = Build([S2, S3, S4], Task("SHIP-1", At(10, 8, 12), 10), Task("SHIP-2", At(10, 21, 12), 40), Task("SHIP-3", At(9, 24, 12), 90));

        (view.Headline.Finished?.PeriodId, view.Headline.Finished?.LeadTime.MeanHours, view.Headline.SoFar?.PeriodId, view.Headline.SoFar?.LeadTime.MeanHours)
            .ShouldBe((S3.Id, 10d, S4.Id, 40d));
        view.Periods.Select(p => p.InProgress).ShouldBe([false, false, true]);
    }

    // Mutation: sum only the shown sprints of the quarter.
    [Test]
    public void Quarters_WhenASprintOfTheQuarterIsNotShown_ShouldStillSumIt_TurnedGreenByB3()
    {
        var view = Build(
            [S3, S4],
            Task("SHIP-1", At(9, 25, 12), 10),
            Task("SHIP-2", At(10, 8, 12), 20),
            Task("SHIP-3", At(10, 9, 12), 30),
            Task("SHIP-4", At(10, 21, 12), 40),
            Task("SHIP-5", At(9, 16, 12), 100));

        view.Quarters.Select(q => (q.Quarter, q.ToDate, q.LeadTime.Tasks, q.LeadTime.MeanHours)).ShouldBe([("Q4 2026", true, 4, 25d)]);
        view.Quarters.Single().PeriodIds.ShouldBe([S2.Id, S3.Id, S4.Id], ignoreOrder: true);
    }

    // Mutation: place the task in the sprint it started in.
    [Test]
    public void Periods_WhenATaskStartedInOneSprintAndEndedInTheNext_ShouldCountItInTheOneItEndedIn_TurnedGreenByB3()
    {
        var view = Build([S2, S3], Task("SHIP-1", At(10, 6, 10), 150));

        view.Periods.Select(p => (p.Id, p.LeadTime.Tasks)).ShouldBe([(S2.Id, 0), (S3.Id, 1)]);
    }

    // Mutation: end a sprint's tasks at its last day, ignoring the weekend after it.
    [Test]
    public void Periods_WhenATaskEndsOnTheWeekendAfterASprint_ShouldCountItInThatSprint_TurnedGreenByB3()
    {
        var view = Build([S2, S3], Task("SHIP-1", At(10, 3, 10), 5));

        view.Periods.Select(p => (p.Id, p.LeadTime.Tasks)).ShouldBe([(S2.Id, 1), (S3.Id, 0)]);
    }
}
