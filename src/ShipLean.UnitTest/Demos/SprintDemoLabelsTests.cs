using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Demos.Reads;

namespace ShipLean.UnitTest.Demos;

[TestFixture]
public class SprintDemoLabelsTests
{
    [TestCase(0, "0:00")]
    [TestCase(8, "0:08")]
    [TestCase(65, "1:05")]
    [TestCase(65.9, "1:05")]
    [TestCase(3599, "59:59")]
    [TestCase(3600, "1:00:00")]
    [TestCase(3723, "1:02:03")]
    public void Duration_ShouldBeMinutesAndSecondsOrHoursFromOneHour(double seconds, string expected)
    {
        SprintDemoLabels.Duration((decimal)seconds).ShouldBe(expected);
    }

    [Test]
    public void When_ShouldNameTodayYesterdayAndTomorrowInTheClocksZone()
    {
#pragma warning disable AG0051 // the labels are checked as text against a fake clock; no real clock is involved.
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 5, 10, 15, 0, TimeSpan.Zero));
#pragma warning restore AG0051
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);

        SprintDemoLabels.When(clock.GetUtcNow(), clock).ShouldBe("today at 10:15");
        SprintDemoLabels.When(clock.GetUtcNow().AddDays(-1), clock).ShouldBe("yesterday at 10:15");
        SprintDemoLabels.When(clock.GetUtcNow().AddDays(1), clock).ShouldBe("tomorrow at 10:15");
        SprintDemoLabels.When(clock.GetUtcNow().AddDays(-5), clock).ShouldBe("31 Oct at 10:15");
    }
}
