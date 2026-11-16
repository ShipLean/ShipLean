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
}
