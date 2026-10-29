using ShipLean.Core.Sprints;
using Shouldly;

namespace ShipLean.UnitTest.Core;

public sealed class SprintDaysTests
{
    private static readonly int[] Weekdays = [1, 2, 3, 4, 5];
    private static readonly DateOnly Monday = new(2099, 1, 5);

    [Test]
    public void Count_WhenTwoWeeks_ShouldHaveTenWorkingDays()
    {
        var days = SprintDays.Count(Monday, Monday.AddDays(13), Weekdays, []);

        days.WorkingDays.ShouldBe(10);
        days.Holidays.ShouldBeEmpty();
        days.DefaultDays.ShouldBe(10);
    }

    [Test]
    public void Count_WhenHolidayOnAWorkingDay_ShouldKeepItInWButTakeItFromTheDefault()
    {
        var days = SprintDays.Count(Monday, Monday.AddDays(13), Weekdays, [Monday.AddDays(2)]);

        days.WorkingDays.ShouldBe(10);
        days.Holidays.ShouldBe([Monday.AddDays(2)]);
        days.DefaultDays.ShouldBe(9);
    }

    [Test]
    public void Count_WhenHolidayOnAWeekend_ShouldNotCountIt()
    {
        var days = SprintDays.Count(Monday, Monday.AddDays(13), Weekdays, [Monday.AddDays(5), Monday.AddDays(6)]);

        days.Holidays.ShouldBeEmpty();
        days.DefaultDays.ShouldBe(10);
    }

    [Test]
    public void Count_WhenHolidayOutsideTheDatesOrListedTwice_ShouldCountOnlyInsideOnce()
    {
        var days = SprintDays.Count(Monday, Monday.AddDays(13), Weekdays, [Monday.AddDays(-1), Monday.AddDays(14), Monday.AddDays(1), Monday.AddDays(1)]);

        days.Holidays.ShouldBe([Monday.AddDays(1)]);
    }

    [Test]
    public void Count_WhenSaturdayIsAWorkingDay_ShouldCountItAndItsHoliday()
    {
        var days = SprintDays.Count(Monday, Monday.AddDays(6), [1, 2, 3, 4, 5, 6], [Monday.AddDays(5)]);

        days.WorkingDays.ShouldBe(6);
        days.DefaultDays.ShouldBe(5);
    }

    [Test]
    public void Count_WhenNoDateIsAWorkingDay_ShouldBeZero()
    {
        var days = SprintDays.Count(Monday.AddDays(5), Monday.AddDays(6), Weekdays, [Monday.AddDays(5)]);

        days.WorkingDays.ShouldBe(0);
        days.DefaultDays.ShouldBe(0);
    }

    [Test]
    public void Count_WhenOneDaySprint_ShouldCountThatDay()
    {
        SprintDays.Count(Monday, Monday, Weekdays, []).WorkingDays.ShouldBe(1);
    }
}
