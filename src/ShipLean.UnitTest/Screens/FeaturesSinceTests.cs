using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.FeaturesOverview;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Screens;

// FeaturesSince in a +07:00 zone, now at 17:00 local (10:00 UTC) today: the labels follow the zone's calendar days and clock.
[TestFixture]
public class FeaturesSinceTests
{
    private static readonly TimeZoneInfo Plus7 = TimeZoneInfo.CreateCustomTimeZone("Test+07", TimeSpan.FromHours(7), "Test+07", "Test+07");

    // Local midnight today is 17:00 UTC the day before TestDates.Today.
    private static DateTimeOffset LocalMidnight => TestDates.Today.AddHours(-7);

    private static FakeTimeProvider Clock()
    {
        var clock = new FakeTimeProvider(TestDates.Today.AddHours(10));
        clock.SetLocalTimeZone(Plus7);
        return clock;
    }

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

        var (since, label) = FeaturesSince.Of(seenAt, Clock());

        since.ShouldBe(seenAt);
        label.ShouldBe(string.Format(CultureInfo.InvariantCulture, expected, weekday));
    }

    [Test]
    public void Since_WhenNeverSeen_ShouldBe24HoursBack()
    {
        var clock = Clock();

        var (since, label) = FeaturesSince.Of(null, clock);

        since.ShouldBe(clock.GetUtcNow().AddHours(-24));
        label.ShouldBe("since yesterday");
    }
}
