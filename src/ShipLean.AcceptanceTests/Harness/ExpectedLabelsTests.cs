using ShipLean.TestSupport.Identity;

namespace ShipLean.AcceptanceTests.Harness;

// ExpectedLabels against literals, so the labels acceptance tests expect are pinned independently of DisplayDate. No
// browser or host.
[TestFixture]
public class ExpectedLabelsTests
{
    private static readonly DateTimeOffset ZeroTwoUtc = new(2025, 3, 2, 0, 2, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LateEveningUtc = new(2025, 3, 2, 20, 0, 0, TimeSpan.Zero);

    [Test]
    public void Time_WhenZeroTwoUtcInAsiaBangkok_ShouldBeSevenTwo()
    {
        ExpectedLabels.Time(ZeroTwoUtc, ViewerZoneTestData.Bangkok).ShouldBe("07:02");
    }

    [Test]
    public void Time_WhenZeroTwoUtcInUtc_ShouldBeZeroTwo()
    {
        ExpectedLabels.Time(ZeroTwoUtc, TimeZoneInfo.Utc).ShouldBe("00:02");
    }

    [Test]
    public void DayAt_WhenLateEveningUtcLastYearOrBefore_ShouldReadTheNextDayInAsiaBangkok()
    {
        ExpectedLabels.DayAt(LateEveningUtc, ViewerZoneTestData.Bangkok).ShouldBe("3 Mar 2025 at 03:00");
        ExpectedLabels.DayAt(LateEveningUtc, TimeZoneInfo.Utc).ShouldBe("2 Mar 2025 at 20:00");
    }

    [Test]
    public void Relative_WhenLateEveningUtcLastYearOrBefore_ShouldBeTheShortDayInTheZone()
    {
        ExpectedLabels.Relative(LateEveningUtc, ViewerZoneTestData.Bangkok).ShouldBe("3 Mar 2025");
        ExpectedLabels.Relative(LateEveningUtc).ShouldBe("2 Mar 2025");
    }

    [Test]
    public void Relative_WhenJustNow_ShouldBeJustNow()
    {
        ExpectedLabels.JustNow().ShouldBe("just now");
    }
}
