using ShipLean.Core;

namespace ShipLean.UnitTest;

[TestFixture]
public class SprintLabelsTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    [TestCase("2026-10-20", "2026-11-02", SprintPhase.Future, "Starts in 1 day", "20 Oct – 2 Nov")]
    [TestCase("2026-10-12", "2026-10-25", SprintPhase.Current, "6 days left", "12 Oct – 25 Oct")]
    [TestCase("2026-10-06", "2026-10-19", SprintPhase.Current, "Ends today", "6 Oct – 19 Oct")]
    [TestCase("2026-10-19", "2026-10-30", SprintPhase.Current, "11 days left", "19 Oct – 30 Oct")]
    [TestCase("2026-09-21", "2026-10-04", SprintPhase.Past, "Ended 15 days ago", "21 Sep – 4 Oct")]
    [TestCase("2026-12-28", "2027-01-10", SprintPhase.Future, "Starts in 70 days", "28 Dec 2026 – 10 Jan 2027")]
    public void Labels_WhenDatesRelativeToToday_ShouldReadLikeTheDesign(string start, string end, SprintPhase phase, string time, string dates)
    {
        var from = DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture);
        var to = DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture);

        SprintLabels.Phase(from, to, Today).ShouldBe(phase);
        SprintLabels.Time(from, to, Today).ShouldBe(time);
        SprintLabels.Dates(from, to, Today).ShouldBe(dates);
    }

    [Test]
    public void Today_WhenLocalZoneIsAhead_ShouldStillBeTheUtcDay()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 19, 23, 30, 0, TimeSpan.Zero), TimeZoneInfo.CreateCustomTimeZone("plus10", TimeSpan.FromHours(10), "plus10", "plus10"));

        SprintLabels.Today(clock).ShouldBe(Today);
    }

    private sealed class FixedClock(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public override TimeZoneInfo LocalTimeZone => zone;
    }
}
