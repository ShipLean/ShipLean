using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.FeaturesOverview;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Identity;

namespace ShipLean.UnitTest.Screens;

// FeaturesSince in a +07:00 zone, now at 17:00 local (10:00 UTC) today: the labels follow the zone's calendar days and clock.
[TestFixture]
public class FeaturesSinceTests
{
    private static readonly TimeZoneInfo Plus7 = TimeZoneInfo.CreateCustomTimeZone("Test+07", TimeSpan.FromHours(7), "Test+07", "Test+07");

    // Local midnight today is 17:00 UTC the day before TestDates.Today.
    private static DateTimeOffset LocalMidnight => TestDates.Today.AddHours(-7);

    // The clock's own zone stays UTC: only the zone passed in decides the labels.
    private static FakeTimeProvider Clock() => new(TestDates.Today.AddHours(10));

    [TestCase(0, 9 * 60 + 12, "since 09:12")]
    [TestCase(0, 0, "since 00:00")]
    [TestCase(0, 18 * 60, "since 18:00")]
    [TestCase(-1, 9 * 60 + 12, "since yesterday")]
    [TestCase(-1, 23 * 60 + 59, "since yesterday")]
    [TestCase(-1, 0, "since yesterday")]
    [TestCase(-2, 23 * 60 + 59, "since {0} 23:59")]
    [TestCase(-6, 9 * 60 + 12, "since {0} 09:12")]
    public void SinceLabel_WhenYesterdayTodayOrEarlier_ShouldReadAsDesigned(int days, int minutes, string expected)
    {
        var seenAt = LocalMidnight.AddDays(days).AddMinutes(minutes);
        var weekday = LocalMidnight.AddDays(days).AddHours(7).ToString("ddd", CultureInfo.InvariantCulture);

        var (since, label) = FeaturesSince.Of(seenAt, Clock(), Plus7);

        since.ShouldBe(seenAt);
        label.ShouldBe(string.Format(CultureInfo.InvariantCulture, expected, weekday));
    }

    [Test]
    public void Since_WhenNeverSeen_ShouldBe24HoursBack()
    {
        var clock = Clock();

        var (since, label) = FeaturesSince.Of(null, clock, Plus7);

        since.ShouldBe(clock.GetUtcNow().AddHours(-24));
        label.ShouldBe("since yesterday");
    }

    // Mutation: pass TimeZoneInfo.Utc to FeaturesSince's DaysUntil, Time or ShortWeekday. Now is Mon 2026-11-16 10:00 UTC; the change
    // was Thu 2026-11-12 20:00 UTC, which is Fri 03:00 in Bangkok.
    [Test]
    public void Since_WhenViewerIsInBangkok_ShouldNameTheBangkokTimeAndWeekday()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var seenAt = new DateTimeOffset(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);

        var (_, bangkok) = FeaturesSince.Of(seenAt, clock, ViewerZoneTestData.Bangkok);
        var (_, utc) = FeaturesSince.Of(seenAt, clock, TimeZoneInfo.Utc);

        bangkok.ShouldBe("since Fri 03:00");
        utc.ShouldBe("since Thu 20:00");
    }
}
