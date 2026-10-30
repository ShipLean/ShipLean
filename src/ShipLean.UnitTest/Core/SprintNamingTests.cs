using ShipLean.Application.Features.Mcp.Sprint;
using ShipLean.Core.Sprints;

namespace ShipLean.UnitTest.Core;

[TestFixture]
public class SprintNamingTests
{
    private static string Quarter(DateOnly start, int length, params string[] taken)
        => SprintNaming.Name(SprintNamingConvention.YearQuarterNumber, start, length, taken.Contains, 99);

    [Test]
    public void Name_WhenStartIsTheFirstDayOfAQuarter_ShouldBeSprintOne()
    {
        Quarter(new DateOnly(2026, 10, 1), 14).ShouldBe("2026 Q4 Sprint 1");
        Quarter(new DateOnly(2027, 1, 1), 14).ShouldBe("2027 Q1 Sprint 1");
    }

    [TestCase(2026, 10, 14, "2026 Q4 Sprint 1")]
    [TestCase(2026, 10, 15, "2026 Q4 Sprint 2")]
    [TestCase(2026, 12, 31, "2026 Q4 Sprint 7")]
    public void Name_WhenStartIsLaterInTheQuarter_ShouldCountWholeLengths(int year, int month, int day, string expected)
    {
        // Offsets from 1 October are 13, 14 and 91.
        Quarter(new DateOnly(year, month, day), 14).ShouldBe(expected);
    }

    [Test]
    public void Name_WhenStartIsInTheQuartersLeftoverDays_ShouldTakeTheNextNumber()
    {
        // Day 28 of Q1 2027 starts the 3rd fortnight, day 89 is still the 7th.
        Quarter(new DateOnly(2027, 1, 29), 14).ShouldBe("2027 Q1 Sprint 3");
        Quarter(new DateOnly(2027, 3, 31), 14).ShouldBe("2027 Q1 Sprint 7");
    }

    [Test]
    public void Name_WhenSprintCrossesAQuarterBoundary_ShouldUseTheQuarterOfItsStart()
    {
        Quarter(new DateOnly(2026, 9, 28), 14).ShouldBe("2026 Q3 Sprint 7");
        Quarter(new DateOnly(2026, 12, 28), 14).ShouldBe("2026 Q4 Sprint 7");
    }

    [TestCase(7, "2026 Q4 Sprint 4")]
    [TestCase(14, "2026 Q4 Sprint 2")]
    [TestCase(21, "2026 Q4 Sprint 2")]
    public void Name_WhenLengthChanges_ShouldDivideByIt(int length, string expected)
    {
        // 22 days after the quarter start.
        Quarter(new DateOnly(2026, 10, 23), length).ShouldBe(expected);
    }

    [Test]
    public void Name_WhenLengthIsOneDay_ShouldCountDaysIntoTheQuarter()
    {
        Quarter(new DateOnly(2026, 10, 1), 1).ShouldBe("2026 Q4 Sprint 1");
        Quarter(new DateOnly(2026, 10, 10), 1).ShouldBe("2026 Q4 Sprint 10");
    }

    [Test]
    public void Name_WhenLengthIsNotPositive_ShouldNotDivideByZero()
    {
        Quarter(new DateOnly(2026, 10, 5), 0).ShouldBe("2026 Q4 Sprint 5");
    }

    [Test]
    public void Name_WhenTheNameIsTaken_ShouldBumpTheNumberUntilFree()
    {
        Quarter(new DateOnly(2026, 10, 1), 14, "2026 Q4 Sprint 1", "2026 Q4 Sprint 2").ShouldBe("2026 Q4 Sprint 3");
    }

    [Test]
    public void Name_WhenNumber_ShouldUseTheGivenNumberAndBumpWhenTaken()
    {
        var start = new DateOnly(2026, 10, 1);

        SprintNaming.Name(SprintNamingConvention.Number, start, 14, _ => false, 27).ShouldBe("Sprint 27");
        SprintNaming.Name(SprintNamingConvention.Number, start, 14, name => name is "Sprint 27" or "Sprint 28", 27).ShouldBe("Sprint 29");
    }

    [Test]
    public void NextNumber_ShouldBeOnePastTheHighestTrailingNumber()
    {
        SprintNaming.NextNumber(["Sprint 4", "Sprint 25", "2026 Q4 Sprint 3"]).ShouldBe(26);
    }

    [Test]
    public void NextNumber_WhenNoNameEndsInANumber_ShouldBeTheCountPlusOne()
    {
        SprintNaming.NextNumber(["Alpha", "Beta"]).ShouldBe(3);
        SprintNaming.NextNumber([]).ShouldBe(1);
        SprintNaming.NextNumber(["Build 20261024123456"]).ShouldBe(2);
    }

    [Test]
    public void FromColumn_ShouldRoundTripToColumn()
    {
        SprintNaming.FromColumn(SprintNaming.ToColumn(SprintNamingConvention.YearQuarterNumber)).ShouldBe(SprintNamingConvention.YearQuarterNumber);
        SprintNaming.FromColumn("number").ShouldBe(SprintNamingConvention.Number);
    }

    [Test]
    public void Sequence_ShouldStepByTheLengthAndNeverRepeat()
    {
        var names = new SprintNameSequence(SprintNamingConvention.YearQuarterNumber, ["2026 Q4 Sprint 3"], new DateOnly(2026, 10, 29), 14, 1);

        names.Next().ShouldBe("2026 Q4 Sprint 4");
        names.Next().ShouldBe("2026 Q4 Sprint 5");
    }

    [Test]
    public void Sequence_WhenAnExistingNameDiffersOnlyInCase_ShouldTreatItAsTaken()
    {
        var names = new SprintNameSequence(SprintNamingConvention.YearQuarterNumber, ["2026 q4 sprint 1"], new DateOnly(2026, 10, 1), 14, 1);

        names.Next().ShouldBe("2026 Q4 Sprint 2");
    }

    [Test]
    public void McpNext_WhenALiveNameDiffersOnlyInCase_ShouldTreatItAsTaken()
    {
        var number = SprintNames.Next(SprintNamingConvention.Number, new DateOnly(2026, 10, 29), 14, ["sprint 8", "sprint 9"], "sprint 8", 2);
        var quarter = SprintNames.Next(SprintNamingConvention.YearQuarterNumber, new DateOnly(2026, 10, 1), 14, ["2026 q4 sprint 1"], "2026 q4 sprint 1", 1);

        number.ShouldBe("sprint 10");
        quarter.ShouldBe("2026 Q4 Sprint 2");
    }
}
