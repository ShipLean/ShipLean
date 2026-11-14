using ShipLean.Core;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest;

[TestFixture]
public class DisplayDateTests
{
    [TestCase("2026-12-25", "Fri 25 Dec")]
    [TestCase("2099-01-07", "Wed 7 Jan")]
    public void WeekdayDay_WhenDate_ReturnsWeekdayDayMonth(string day, string expected)
    {
        DisplayDate.WeekdayDay(DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(expected);
    }

    [TestCase("2026-10-05T09:10:00+00:00", "Mon 5 Oct, 09:10")]
    [TestCase("2026-10-05T23:30:00+00:00", "Tue 6 Oct, 06:30")]
    public void DayTime_WhenInstant_ShouldReadInTheZone(string instant, string expected)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test+07", TimeSpan.FromHours(instant.Contains("23:30") ? 7 : 0), "Test", "Test");

        DisplayDate.DayTime(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture), zone).ShouldBe(expected);
    }

    [Test]
    public void DayLabel_WhenInstant_ShouldNameTheWeekdayAndDay()
    {
        DisplayDate.DayLabel(new DateTimeOffset(2026, 8, 18, 10, 20, 0, TimeSpan.Zero), TimeZoneInfo.Utc).ShouldBe("Tue 18 Aug");
    }

    [TestCase(0.75, "45 min")]
    [TestCase(2.5, "2.5 h")]
    [TestCase(23, "23 h")]
    [TestCase(1.3, "1.3 h")]
    [TestCase(24.8, "1 day")]
    [TestCase(25.1667, "1 day")]
    [TestCase(46, "1.9 days")]
    [TestCase(103, "4.3 days")]
    [TestCase(244.5, "10.2 days")]
    public void Duration_WhenHours_ShouldPickTheUnit(double hours, string expected)
    {
        DisplayDate.Duration(hours).ShouldBe(expected);
    }

    [TestCase(0.25, "6 hours")]
    [TestCase(0.04, "1 hour")]
    [TestCase(0.8, "19 hours")]
    [TestCase(1.0, "1 day")]
    [TestCase(3.7, "3.7 days")]
    [TestCase(9.96, "10 days")]
    [TestCase(16.04, "16 days")]
    [TestCase(44.3, "44 days")]
    public void Gap_WhenDays_ShouldPickTheUnit(double days, string expected)
    {
        DisplayDate.Gap(days).ShouldBe(expected);
    }

    // 20:00 UTC is 03:00 the next day at +07:00, so the weekday is the next day's.
    [Test]
    public void Weekday_WhenTheZoneIsOnTheNextDay_ShouldNameTheLocalWeekday()
    {
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(TestDates.Today);
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test+07", TimeSpan.FromHours(7), "Test+07", "Test+07"));
        var value = TestDates.Today.AddHours(20);

        DisplayDate.Weekday(value, clock).ShouldBe(TestDates.Today.AddDays(1).ToString("ddd", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Test]
    public void Age_WhenUnderAnHour_ShouldSayNow()
    {
        var now = TestDates.Today.AddHours(12);

        DisplayDate.Age(now.AddMinutes(-59), now).ShouldBe("now");
    }

    [Test]
    public void Age_WhenOneHour_ShouldSay1h()
    {
        var now = TestDates.Today.AddHours(12);

        DisplayDate.Age(now.AddHours(-1), now).ShouldBe("1h");
    }

    [Test]
    public void Age_When23Hours_ShouldSay23h()
    {
        var now = TestDates.Today.AddHours(12);

        DisplayDate.Age(now.AddHours(-23).AddMinutes(-59), now).ShouldBe("23h");
    }

    [Test]
    public void Age_WhenOneDay_ShouldSay1d()
    {
        var now = TestDates.Today.AddHours(12);

        DisplayDate.Age(now.AddDays(-1), now).ShouldBe("1d");
    }

    [Test]
    public void Age_WhenSixDays_ShouldSay6d()
    {
        var now = TestDates.Today.AddHours(12);

        DisplayDate.Age(now.AddDays(-6).AddHours(-23), now).ShouldBe("6d");
    }

    [Test]
    public void Age_WhenSevenDays_ShouldSay1w()
    {
        var now = TestDates.Today.AddHours(12);

        DisplayDate.Age(now.AddDays(-7), now).ShouldBe("1w");
        DisplayDate.Age(now.AddDays(-20), now).ShouldBe("2w");
    }

    // Off the boundary: a value just under the next unit rounds down, never up.
    [Test]
    public void Age_WhenOneHourFiftyNineMinutes_ShouldSay1h()
    {
        var now = TestDates.Today.AddHours(12);

        DisplayDate.Age(now.AddHours(-1).AddMinutes(-59), now).ShouldBe("1h");
    }

    [Test]
    public void Age_WhenOneDayTwentyThreeHours_ShouldSay1d()
    {
        var now = TestDates.Today.AddHours(12);

        DisplayDate.Age(now.AddDays(-1).AddHours(-23), now).ShouldBe("1d");
    }

    [Test]
    public void Age_WhenThirteenDays_ShouldSay1w()
    {
        var now = TestDates.Today.AddHours(12);

        DisplayDate.Age(now.AddDays(-13), now).ShouldBe("1w");
    }

    [Test]
    public void Format_WhenUtc_ReturnsMinutePrecisionUtcLabel()
    {
        var value = new DateTimeOffset(2026, 1, 2, 3, 4, 59, TimeSpan.Zero);

        DisplayDate.Format(value).ShouldBe("2026-01-02 03:04 UTC");
    }

    [Test]
    public void Format_WhenOffsetGiven_ConvertsToUtcFirst()
    {
        var value = new DateTimeOffset(2026, 1, 2, 3, 4, 0, TimeSpan.FromHours(10));

        DisplayDate.Format(value).ShouldBe("2026-01-01 17:04 UTC");
    }

    [TestCase("2026-10-19T14:59:40Z", "just now")]
    [TestCase("2026-10-19T15:00:30Z", "just now")]
    [TestCase("2026-10-19T14:48:00Z", "12m ago")]
    [TestCase("2026-10-19T13:00:00Z", "2h ago")]
    [TestCase("2026-10-19T00:00:00Z", "15h ago")]
    [TestCase("2026-10-18T23:59:00Z", "Yesterday")]
    [TestCase("2026-10-18T00:00:00Z", "Yesterday")]
    [TestCase("2026-09-30T10:00:00Z", "30 Sep")]
    [TestCase("2026-01-01T00:00:00Z", "1 Jan")]
    [TestCase("2025-03-02T10:00:00Z", "2 Mar 2025")]
    public void Relative_WhenEditedAt_ShouldReadLikeTheLibrary(string edited, string expected)
    {
        var now = DateTimeOffset.Parse("2026-10-19T15:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        DisplayDate.Relative(DateTimeOffset.Parse(edited, System.Globalization.CultureInfo.InvariantCulture), now, TimeZoneInfo.Utc)
            .ShouldBe(expected);
    }

    [Test]
    public void Relative_WhenZoneIsAhead_ShouldUseItsCalendarDay()
    {
        var now = new DateTimeOffset(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus10", TimeSpan.FromHours(10), "plus10", "plus10");

        // Midnight UTC on the 19th is 10:00 local on the 19th, and now is 01:00 local on the 20th
        // (in UTC the same pair reads "15h ago").
        DisplayDate.Relative(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero), now, zone).ShouldBe("Yesterday");
    }

    [Test]
    public void Today_WhenZoneIsAhead_ShouldBeItsCalendarDay()
    {
        var now = new DateTimeOffset(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus10", TimeSpan.FromHours(10), "plus10", "plus10");

        DisplayDate.Today(now, TimeZoneInfo.Utc).ShouldBe(new DateOnly(2026, 10, 19));
        DisplayDate.Today(now, zone).ShouldBe(new DateOnly(2026, 10, 20));
    }

    [TestCase("2026-10-19T15:00:00Z", 0)]
    [TestCase("2026-10-19T23:59:00Z", 0)]
    [TestCase("2026-10-20T00:00:00Z", 1)]
    [TestCase("2026-10-29T08:00:00Z", 10)]
    [TestCase("2026-10-18T23:59:00Z", -1)]
    public void DaysUntil_WhenDueAt_ShouldCountCalendarDays(string due, int expected)
    {
        var now = new DateTimeOffset(2026, 10, 19, 15, 0, 0, TimeSpan.Zero);

        DisplayDate.DaysUntil(DateTimeOffset.Parse(due, System.Globalization.CultureInfo.InvariantCulture), now, TimeZoneInfo.Utc)
            .ShouldBe(expected);
    }

    [Test]
    public void StartOfDay_WhenNoTransition_ShouldBeLocalMidnightInUtc()
    {
        DisplayDate.StartOfDay(new DateOnly(2026, 6, 1), MidnightTransitions())
            .ShouldBe(new DateTimeOffset(2026, 6, 1, 3, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public void StartOfDay_WhenMidnightIsSkipped_ShouldBeTheTransition()
    {
        // Clocks go from 00:00 (-03) to 01:00 (-02) on 1 Nov, so the day starts at 03:00Z, which reads 01:00 locally.
        var start = DisplayDate.StartOfDay(new DateOnly(2026, 11, 1), MidnightTransitions());

        start.ShouldBe(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero));
        start.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Test]
    public void StartOfDay_WhenMidnightHappensTwice_ShouldBeTheFirstMidnight()
    {
        // Clocks go from 01:00 (-02) back to 00:00 (-03) on 21 Feb, so midnight happens at 02:00Z and again at 03:00Z.
        DisplayDate.StartOfDay(new DateOnly(2026, 2, 21), MidnightTransitions())
            .ShouldBe(new DateTimeOffset(2026, 2, 21, 2, 0, 0, TimeSpan.Zero));
    }

    // A -03 zone with summer time (-02) from midnight on 1 Nov until 01:00 summer time on 21 Feb: both transitions touch
    // midnight, as some real zones' do.
    private static TimeZoneInfo MidnightTransitions()
    {
#pragma warning disable AG0051 // the bounds of a made-up zone, tested on fixed dates inside them; no clock reads them.
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), new DateTime(2030, 12, 31, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), 11, 1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0, DateTimeKind.Unspecified), 2, 21));
#pragma warning restore AG0051
        return TimeZoneInfo.CreateCustomTimeZone("midnight-dst", TimeSpan.FromHours(-3), "midnight-dst", "standard", "summer", [rule]);
    }
}
