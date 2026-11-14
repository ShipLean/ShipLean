using ShipLean.Application.Features.Timeline.Build;
using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport.Timeline;

namespace ShipLean.UnitTest.Timeline;

// The bars builder's rules (C1, C3, D2 to D5, D16): periods, bars, brackets, not started, key dates, gaps and the scale.
[TestFixture]
public class TimelineBarsTests
{
    private static readonly DateTimeOffset T0 = TimelineFactsBuilder.Start;
    private static readonly DateTimeOffset AsOf = T0.AddDays(10);
    private static readonly Guid Ana = new("00000000-0000-4000-8000-0000000000a1");
    private static readonly Guid Tom = new("00000000-0000-4000-8000-0000000000a2");

    private static TimelineFactsBuilder Facts() =>
        new TimelineFactsBuilder().WithPerson(Ana, "Ana Costa").WithPerson(Tom, "Tom Okafor").WithStory("SHIP-1", "Story", Tom).WithTask("SHIP-2", "SHIP-1", "Task", Ana);

    private static TimelineBarsPart Build(TimelineFactsBuilder facts, TimeZoneInfo? zone = null) => TimelineBars.Build(facts.Build(), AsOf, zone ?? TimeZoneInfo.Utc);

    private static TimelineBarDto OnlyBar(TimelineBarsPart part) => part.Bars.Single();

    private static void Active(TimelineFactsBuilder f, string key, double atHours) => f.WithMove(key, T0.AddHours(atHours), "Active", StateCategory.InProgress);

    private static void Resolved(TimelineFactsBuilder f, string key, double atHours) => f.WithMove(key, T0.AddHours(atHours), "Resolved", StateCategory.Resolved);

    [Test]
    public void Build_WhenReopenedAfterReview_ShouldEndAtLastResolved()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);
        Resolved(f, "SHIP-2", 12);
        Active(f, "SHIP-2", 30);
        Resolved(f, "SHIP-2", 33);

        var bar = OnlyBar(Build(f));

        bar.Start.ShouldBe(T0.AddHours(9));
        bar.End.ShouldBe(T0.AddHours(33));
        bar.State.ShouldBe(TimelineBarState.Resolved);
        bar.History.Select(h => h.State).ShouldBe([TimelinePeriodState.Active, TimelinePeriodState.Resolved, TimelinePeriodState.Active, TimelinePeriodState.Resolved]);
    }

    [Test]
    public void Build_WhenReopenedAndNotResolvedAgain_ShouldBeInFlight()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);
        Resolved(f, "SHIP-2", 12);
        Active(f, "SHIP-2", 30);

        var bar = OnlyBar(Build(f));

        bar.State.ShouldBe(TimelineBarState.InFlight);
        bar.End.ShouldBeNull();
        bar.EndHours.ShouldBeNull();
        bar.Hours.ShouldBe(231);
        bar.History[^1].To.ShouldBe(AsOf);
    }

    [Test]
    public void Build_WhenResolvedThenClosed_ShouldEndAtResolved()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);
        Resolved(f, "SHIP-2", 12);
        f.WithMove("SHIP-2", T0.AddHours(40), "Closed", StateCategory.Completed);

        var bar = OnlyBar(Build(f));

        bar.End.ShouldBe(T0.AddHours(12));
        bar.State.ShouldBe(TimelineBarState.Closed);
        bar.History.Select(h => h.State).ShouldBe([TimelinePeriodState.Active, TimelinePeriodState.Resolved, TimelinePeriodState.Closed]);
        bar.History[^1].To.ShouldBeNull();
    }

    [Test]
    public void Build_WhenClosedWithoutResolved_ShouldEndAtClosed()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);
        f.WithMove("SHIP-2", T0.AddHours(20), "Closed", StateCategory.Completed);

        var bar = OnlyBar(Build(f));

        bar.End.ShouldBe(T0.AddHours(20));
        bar.State.ShouldBe(TimelineBarState.Closed);
        bar.Inferred.ShouldBeFalse();
    }

    [Test]
    public void Build_WhenBlockedTagSetWhileActive_ShouldSplitABlockedPeriod()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);
        f.WithBlockedTag("SHIP-2", T0.AddHours(12), true).WithBlockedTag("SHIP-2", T0.AddHours(20), false);
        Resolved(f, "SHIP-2", 22);

        var history = OnlyBar(Build(f)).History;

        history.Select(h => h.State).ShouldBe([TimelinePeriodState.Active, TimelinePeriodState.Blocked, TimelinePeriodState.Active, TimelinePeriodState.Resolved]);
        history[1].StateName.ShouldBe("Active");
        history[1].Hours.ShouldBe(8);
        history[1].DurationLabel.ShouldBe("8 h");
        history[1].FromLabel.ShouldBe("Mon 5 Oct, 12:00");
    }

    [Test]
    public void Build_WhenTwoBlockedPeriods_ShouldShowBoth()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);
        f.WithBlockedTag("SHIP-2", T0.AddHours(10), true).WithBlockedTag("SHIP-2", T0.AddHours(12), false)
            .WithBlockedTag("SHIP-2", T0.AddHours(14), true).WithBlockedTag("SHIP-2", T0.AddHours(16), false);
        Resolved(f, "SHIP-2", 18);

        OnlyBar(Build(f)).History.Count(h => h.State == TimelinePeriodState.Blocked).ShouldBe(2);
    }

    [Test]
    public void Build_WhenBlockedNeverCleared_ShouldRunToAsOf()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);
        f.WithBlockedTag("SHIP-2", T0.AddHours(12), true);

        var last = OnlyBar(Build(f)).History[^1];

        last.State.ShouldBe(TimelinePeriodState.Blocked);
        last.To.ShouldBe(AsOf);
    }

    [Test]
    public void Build_WhenClosedWithoutStart_ShouldBeInferredDiamond()
    {
        var f = Facts();
        f.WithMove("SHIP-2", T0.AddHours(30), "Closed", StateCategory.Completed);

        var bar = OnlyBar(Build(f));

        bar.Inferred.ShouldBeTrue();
        bar.Start.ShouldBeNull();
        bar.StartHours.ShouldBeNull();
        bar.Hours.ShouldBeNull();
        bar.DurationLabel.ShouldBeNull();
        bar.End.ShouldBe(T0.AddHours(30));
        bar.EndHours.ShouldBe(6);
        bar.EndLabel.ShouldBe("Tue 6 Oct, 06:00");
        bar.History.Single().State.ShouldBe(TimelinePeriodState.Closed);
    }

    [Test]
    public void Build_WhenNeverStarted_ShouldBeNotStarted()
    {
        var part = Build(Facts().WithStory("SHIP-3", "Idle story"));

        part.Bars.ShouldBeEmpty();
        part.NotStarted.Select(n => (n.Key, n.Type, n.Parent)).ShouldBe([("SHIP-3", WorkItemTypeId.Story, (string?)null), ("SHIP-2", WorkItemTypeId.Task, "SHIP-1")]);
    }

    [Test]
    public void Build_WhenRemoved_ShouldLeaveItOut()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);
        f.WithMove("SHIP-2", T0.AddHours(12), "Removed", StateCategory.Removed);

        var part = Build(f);

        part.Bars.ShouldBeEmpty();
        part.NotStarted.Select(n => n.Key).ShouldNotContain("SHIP-2");
    }

    [Test]
    public void Build_WhenStoryHasOpenTask_ShouldLeaveBracketEndNull()
    {
        var f = Facts().WithTask("SHIP-3", "SHIP-1");
        Active(f, "SHIP-2", 9);
        Resolved(f, "SHIP-2", 12);
        Active(f, "SHIP-3", 10);

        var story = Build(f).Stories.Single();

        story.Start.ShouldBe(T0.AddHours(9));
        story.End.ShouldBeNull();
        story.Hours.ShouldBe(231);
        story.Tasks.ShouldBe(["SHIP-2", "SHIP-3"]);
    }

    [Test]
    public void Build_WhenStoryHasInferredChild_ShouldEndAtItsDiamond()
    {
        var f = Facts().WithTask("SHIP-3", "SHIP-1");
        Active(f, "SHIP-2", 9);
        Resolved(f, "SHIP-2", 12);
        f.WithMove("SHIP-3", T0.AddHours(50), "Closed", StateCategory.Completed);

        var story = Build(f).Stories.Single();

        story.Start.ShouldBe(T0.AddHours(9));
        story.End.ShouldBe(T0.AddHours(50));
    }

    [Test]
    public void Build_WhenStoryAllNotStarted_ShouldHaveNullBracket()
    {
        var part = Build(Facts());

        var story = part.Stories.Single();
        story.Start.ShouldBeNull();
        story.End.ShouldBeNull();
        story.Hours.ShouldBeNull();
        story.Tasks.ShouldBeEmpty();
        part.NotStarted.Single().Key.ShouldBe("SHIP-2");
    }

    [Test]
    public void Build_WhenNoPlanCaptured_ShouldTakePlannedFromFirstChild()
    {
        var f = Facts().WithStory("SHIP-5", "Later", null, T0.AddDays(3));

        var planned = Build(f).KeyDates[1];

        planned.At.ShouldBe(T0);
        planned.Source.ShouldBe("First story added");
    }

    [Test]
    public void Build_WhenPlanCaptured_ShouldUseIt()
    {
        var planned = Build(Facts().WithPlanApplied(T0.AddHours(5))).KeyDates[1];

        planned.At.ShouldBe(T0.AddHours(5));
        planned.Source.ShouldBe("Plan captured");
    }

    [Test]
    public void Build_WhenNoStoriesAndNoPlan_ShouldLeavePlannedNull()
    {
        var planned = TimelineBars.Build(new TimelineFactsBuilder().Build(), AsOf, TimeZoneInfo.Utc).KeyDates[1];

        planned.At.ShouldBeNull();
        planned.Source.ShouldBeNull();
    }

    [Test]
    public void Build_WhenNothingStarted_ShouldNullLaterGapsWithoutSoFar()
    {
        var part = Build(Facts().WithPlanApplied(T0.AddHours(6)));

        part.Gaps.Count.ShouldBe(3);
        part.Gaps[0].Label.ShouldBe("6 hours");
        part.Gaps[1].SoFar.ShouldBeTrue();
        part.Gaps[2].Days.ShouldBeNull();
        part.Gaps[2].Label.ShouldBeNull();
        part.Gaps[2].SoFar.ShouldBeFalse();
        part.Execution.ShouldBe(new TimelineExecutionDto(null, null, null));
    }

    [Test]
    public void Build_WhenRunning_ShouldMarkLastGapSoFar()
    {
        var f = Facts().WithPlanApplied(T0);
        Active(f, "SHIP-2", 12);

        var part = Build(f);

        part.Finished.ShouldBeFalse();
        part.Gaps[1].Label.ShouldBe("12 hours");
        part.Gaps[2].SoFar.ShouldBeTrue();
        part.Gaps[2].Days.ShouldBe(9.5);
        part.Gaps[2].Label.ShouldBe("9.5 days");
        part.Execution.End.ShouldBeNull();
        part.Execution.CalendarDays.ShouldBe(9.5);
        part.KeyDates[3].At.ShouldBeNull();
    }

    [Test]
    public void Build_WhenFinished_ShouldTakeTheLastCompletedMove()
    {
        var f = Facts().WithFeature("SHIP-99", "Coupons", "Done", StateCategory.Completed, T0, Ana);
        Active(f, "SHIP-2", 9);
        f.WithMove("SHIP-99", T0.AddHours(100), "Done", StateCategory.Completed, null, Tom);

        var part = Build(f);

        part.Finished.ShouldBeTrue();
        part.KeyDates[3].At.ShouldBe(T0.AddHours(100));
        part.KeyDates[3].Source.ShouldBe("Feature moved to Done");
        part.KeyDates[3].By!.Name.ShouldBe("Tom Okafor");
        part.KeyDates[0].By!.Name.ShouldBe("Ana Costa");
        part.KeyDates[0].DayLabel.ShouldBe("Mon 5 Oct");
        part.KeyDates[2].Source.ShouldBe("SHIP-2 moved to Active");
    }

    [Test]
    public void Build_WhenFeatureReopened_ShouldLeaveFinishedNull()
    {
        var f = Facts().WithFeature("SHIP-99", "Coupons", "Active", StateCategory.InProgress);
        Active(f, "SHIP-2", 9);
        f.WithMove("SHIP-99", T0.AddHours(100), "Done", StateCategory.Completed);
        f.WithMove("SHIP-99", T0.AddHours(120), "Active", StateCategory.InProgress);

        var part = Build(f);

        part.Finished.ShouldBeFalse();
        part.KeyDates[3].At.ShouldBeNull();
        part.Scale.NowHours.ShouldNotBeNull();
    }

    [Test]
    public void Build_WhenStartedByAgent_ShouldNameTheAgent()
    {
        var f = Facts();
        f.WithMove("SHIP-2", T0.AddHours(9), "Active", StateCategory.InProgress, "Claude Code", Ana);

        var bar = OnlyBar(Build(f));

        bar.Assignee.ShouldBe(new TimelinePersonDto(null, "Claude Code", true));
        Build(Facts().WithMove("SHIP-2", T0.AddHours(9), "Active", StateCategory.InProgress)).Bars.Single().Assignee!.Name.ShouldBe("Ana Costa");
        Build(f).Stories.Single().Assignee!.Name.ShouldBe("Tom Okafor");
    }

    [Test]
    public void Build_ShouldMarkSaturdayAndSundayAsWeekend()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);

        var days = Build(f).Scale.Days;

        days.Where(d => d.Weekend).Select(d => d.Label).ShouldBe(["Sat 10 Oct", "Sun 11 Oct"]);
        days[0].Label.ShouldBe("Mon 5 Oct");
        days[0].DayOfMonth.ShouldBe("5");
        days.Count.ShouldBe(11);
    }

    [Test]
    public void Build_WhenZoneHasDst_ShouldGiveThatDay23Hours()
    {
        var sydney = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");
        var f = new TimelineFactsBuilder().WithPerson(Ana, "Ana").WithStory("SHIP-1").WithTask("SHIP-2", "SHIP-1", "T", Ana);
        // Saturday 3 Oct 2026 09:00 in Sydney (+10) is 23:00 UTC on 2 Oct; DST starts 02:00 on Sunday 4 Oct.
        f.WithMove("SHIP-2", new DateTimeOffset(2026, 10, 2, 23, 0, 0, TimeSpan.Zero), "Active", StateCategory.InProgress);
        f.WithMove("SHIP-2", new DateTimeOffset(2026, 10, 5, 1, 0, 0, TimeSpan.Zero), "Resolved", StateCategory.Resolved);
        f.WithFeature("SHIP-99", "F", "Done", StateCategory.Completed, T0, null);
        f.WithMove("SHIP-99", new DateTimeOffset(2026, 10, 5, 1, 0, 0, TimeSpan.Zero), "Done", StateCategory.Completed);

        var scale = TimelineBars.Build(f.Build(), AsOf, sydney).Scale;

        scale.Days.Select(d => d.Label).ShouldBe(["Sat 3 Oct", "Sun 4 Oct", "Mon 5 Oct"]);
        scale.Days.Select(d => d.StartHours).ShouldBe([0, 24, 47]);
        scale.EndHours.ShouldBe(47 + 24);
        scale.Start.ShouldBe(new DateTimeOffset(2026, 10, 2, 14, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public void Build_WhenRunning_ShouldSetNowHours()
    {
        var f = Facts();
        Active(f, "SHIP-2", 9);

        var scale = Build(f).Scale;

        scale.NowHours.ShouldBe(240);
        scale.EndHours.ShouldBe(264);
        scale.Start.ShouldBe(T0);
    }

    [Test]
    public void Build_WhenFinished_ShouldNullNowHours()
    {
        var f = Facts().WithFeature("SHIP-99", "F", "Done", StateCategory.Completed);
        Active(f, "SHIP-2", 9);
        f.WithMove("SHIP-99", T0.AddHours(60), "Done", StateCategory.Completed);

        var scale = Build(f).Scale;

        scale.NowHours.ShouldBeNull();
        scale.EndHours.ShouldBe(72);
        scale.Days.Count.ShouldBe(3);
    }

    [Test]
    public void Build_WhenNothingStarted_ShouldDrawAsOfDay()
    {
        var scale = Build(Facts()).Scale;

        scale.Days.Count.ShouldBe(1);
        scale.Days[0].Label.ShouldBe("Thu 15 Oct");
        scale.Start.ShouldBe(T0.AddDays(10));
    }

    [Test]
    public void Build_ShouldClipSprintBandsToTheDaysDrawn()
    {
        var f = Facts().WithSprint("Sprint 1", new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 6))
            .WithSprint("Sprint 2", new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 30))
            .WithSprint("Old", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 14));
        Active(f, "SHIP-2", 9);

        Build(f).Scale.Sprints.ShouldBe([new TimelineSprintDto("Sprint 1", 0, 48), new TimelineSprintDto("Sprint 2", 48, 264)]);
    }

    [Test]
    public void Build_ShouldRoundHoursToOneDecimalAndLabelThem()
    {
        var f = Facts();
        f.WithMove("SHIP-2", T0.AddHours(9).AddMinutes(10), "Active", StateCategory.InProgress);
        f.WithMove("SHIP-2", T0.AddHours(11).AddMinutes(40), "Resolved", StateCategory.Resolved);
        f.WithReview("SHIP-2", T0.AddDays(1).AddHours(10), ReviewVerdict.Approved, Tom, null, "ok");

        var bar = OnlyBar(Build(f));

        bar.StartHours.ShouldBe(9.2);
        bar.EndHours.ShouldBe(11.7);
        bar.Hours.ShouldBe(2.5);
        bar.StartLabel.ShouldBe("Mon 5 Oct, 09:10");
        bar.DurationLabel.ShouldBe("2.5 h");
        bar.Reviews.Single().AtHours.ShouldBe(34);
        bar.Reviews.Single().By.Name.ShouldBe("Tom Okafor");
        bar.Reviews.Single().Note.ShouldBe("ok");
    }

    [Test]
    public void Build_ShouldOrderStoriesByBracketStartWithUnstartedLast()
    {
        var f = new TimelineFactsBuilder().WithStory("SHIP-1").WithStory("SHIP-2").WithStory("SHIP-3")
            .WithTask("SHIP-11", "SHIP-1").WithTask("SHIP-12", "SHIP-2").WithTask("SHIP-13", "SHIP-3");
        Active(f, "SHIP-12", 5);
        Active(f, "SHIP-13", 20);

        var part = Build(f);

        part.Stories.Select(s => s.Key).ShouldBe(["SHIP-2", "SHIP-3", "SHIP-1"]);
        part.Bars.Select(b => b.Key).ShouldBe(["SHIP-12", "SHIP-13"]);
    }
}
