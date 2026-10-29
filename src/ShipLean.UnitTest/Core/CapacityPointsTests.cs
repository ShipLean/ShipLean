using ShipLean.Core.Sprints;
using Shouldly;

namespace ShipLean.UnitTest.Core;

public sealed class CapacityPointsTests
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    public void For_WhenMemberHasAllWorkingDays_ShouldBeAnEqualShareOfVelocity(int members)
    {
        CapacityPoints.For(30m, members, 10m, 10).ShouldBe(decimal.Round(30m / members, 2, MidpointRounding.AwayFromZero));
    }

    [Test]
    public void For_WhenDaysAreZero_ShouldBeZero()
    {
        CapacityPoints.For(30m, 3, 0m, 10).ShouldBe(0m);
    }

    [Test]
    public void For_WhenDaysAreHalfOfW_ShouldBeHalfTheShare()
    {
        CapacityPoints.For(40m, 2, 5m, 10).ShouldBe(10m);
    }

    [Test]
    public void For_WhenHalfDay_ShouldCountIt()
    {
        CapacityPoints.For(20m, 1, 9.5m, 10).ShouldBe(19m);
    }

    [Test]
    public void For_WhenNoWorkingDays_ShouldBeZero()
    {
        CapacityPoints.For(30m, 2, 0m, 0).ShouldBe(0m);
    }

    [Test]
    public void For_WhenNoMembers_ShouldBeZero()
    {
        CapacityPoints.For(30m, 0, 5m, 10).ShouldBe(0m);
    }

    [Test]
    public void For_WhenResultNeedsRounding_ShouldRoundToTwoDecimalsHalfAwayFromZero()
    {
        CapacityPoints.For(10m, 3, 10m, 10).ShouldBe(3.33m);
        CapacityPoints.For(0.05m, 1, 5m, 10).ShouldBe(0.03m);
        CapacityPoints.For(1m, 8, 10m, 10).ShouldBe(0.13m);
    }

    [Test]
    public void For_WhenOverTheLimit_ShouldCapAtLargest()
    {
        CapacityPoints.For(9999.99m, 1, 20m, 10).ShouldBe(9999.99m);
    }

    [Test]
    public void For_WhenOneHolidayInTenDays_ShouldBeNinetyPercentOfTheShare()
    {
        var days = SprintDays.Count(new DateOnly(2099, 1, 5), new DateOnly(2099, 1, 18), [1, 2, 3, 4, 5], [new DateOnly(2099, 1, 7)]);

        CapacityPoints.For(40m, 4, days.DefaultDays, days.WorkingDays).ShouldBe(9m);
    }
}
