using ShipLean.Application.Features.Retros.Rules;
using ShipLean.Contracts.Retros;

namespace ShipLean.UnitTest.Retros;

[TestFixture]
public class RetroRulesTests
{
    [Test]
    public void Mentions_WhenPointsOfAllCategories_ShouldCountOnlyWentBadlyAndNotInPlanPerSprint()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();

        var counts = RetroRules.Mentions([a, b], [(a, RetroCategory.WentBadly), (a, RetroCategory.NotInPlan), (a, RetroCategory.WentWell), (a, RetroCategory.Improve), (b, RetroCategory.Improve)]);

        counts.ShouldBe([2, 0]);
    }

    [TestCase(new[] { 1, 1, 1, 0 }, false, true, TestName = "IsRecurring_When3Of4_ShouldBeTrue")]
    [TestCase(new[] { 0, 1, 1, 0 }, false, false, TestName = "IsRecurring_When2Of4_ShouldBeFalse")]
    [TestCase(new[] { 1, 1, 1, 1 }, true, false, TestName = "IsRecurring_WhenAHelpedFixExists_ShouldBeFalse")]
    [TestCase(new[] { 5, 0, 0, 1, 1 }, false, false, TestName = "IsRecurring_WhenOnlyTheLast4Count_ShouldIgnoreOlderSprints")]
    [TestCase(new[] { 1, 1 }, false, false, TestName = "IsRecurring_WhenFewerThan3Sprints_ShouldBeFalse")]
    public void IsRecurring_WhenCounts_ShouldMatch(int[] counts, bool helped, bool expected)
    {
        RetroRules.IsRecurring(counts, helped).ShouldBe(expected);
    }

    [TestCase(true, true, true, RetroThemeStatus.Recurring)]
    [TestCase(false, true, true, RetroThemeStatus.BeingAddressed)]
    [TestCase(false, false, true, RetroThemeStatus.Resolved)]
    [TestCase(false, false, false, RetroThemeStatus.Quiet)]
    public void Status_WhenFlags_ShouldFollowPrecedence(bool recurring, bool open, bool helped, RetroThemeStatus expected)
    {
        RetroRules.Status(recurring, open, helped).ShouldBe(expected);
    }

    [TestCase(new[] { 4, 2 }, 0, 1, RetroVerdict.Helped, TestName = "Verdict_WhenAfterIsHalfOfBefore_ShouldBeHelped")]
    [TestCase(new[] { 4, 3 }, 0, 1, RetroVerdict.SomeEffect, TestName = "Verdict_WhenJustOverHalf_ShouldBeSomeEffect")]
    [TestCase(new[] { 4, 4 }, 0, 1, RetroVerdict.NoEffect, TestName = "Verdict_WhenAfterEqualsBefore_ShouldBeNoEffect")]
    [TestCase(new[] { 4, 6 }, 0, 1, RetroVerdict.NoEffect, TestName = "Verdict_WhenAfterIsHigher_ShouldBeNoEffect")]
    [TestCase(new[] { 4, 2, 0 }, 1, 1, RetroVerdict.TooEarly, TestName = "Verdict_WhenNoSprintEndedSince_ShouldBeTooEarly")]
    [TestCase(new[] { 4, 2, 0 }, 0, 1, RetroVerdict.Helped, TestName = "Verdict_WhenTheCurrentSprintIsPastTheLastEnded_ShouldNotCountIt")]
    [TestCase(new[] { 0, 1 }, 0, 1, RetroVerdict.NoEffect, TestName = "Verdict_WhenBeforeIsZero_ShouldBeNoEffect")]
    [TestCase(new[] { 0, 0 }, 0, 1, RetroVerdict.NoEffect, TestName = "Verdict_WhenBeforeAndAfterAreZero_ShouldBeNoEffectNotHelped")]
    [TestCase(new[] { 4, 2, 3 }, 0, 2, RetroVerdict.SomeEffect, TestName = "Verdict_WhenAfterIs2Point5OfBefore4_ShouldBeSomeEffect")]
    public void Verdict_WhenCounts_ShouldMatch(int[] counts, int closedAt, int lastEnded, RetroVerdict verdict)
    {
        var result = RetroRules.Verdict(counts, closedAt, lastEnded);

        result.Verdict.ShouldBe(verdict);
    }

    [TestCase(new[] { 4, 2 }, 0, 1, 4d, 2d)]
    [TestCase(new[] { 4, 2, 0 }, 1, 1, 3d, null)]
    [TestCase(new[] { 2, 4, 6, 0 }, 1, 2, 3d, 6d)]
    public void Verdict_WhenCounts_ShouldAverageBeforeAndAfter(int[] counts, int closedAt, int lastEnded, double before, double? after)
    {
        var result = RetroRules.Verdict(counts, closedAt, lastEnded);

        result.BeforeAvg.ShouldBe(before);
        result.AfterAvg.ShouldBe(after);
    }

    [TestCase(new[] { 4, 2 }, 0, 1, -0.5)]
    [TestCase(new[] { 4, 6 }, 0, 1, 0.5)]
    [TestCase(new[] { 0, 1 }, 0, 1, null)]
    [TestCase(new[] { 0, 0 }, 0, 1, null)]
    [TestCase(new[] { 4, 2, 0 }, 1, 1, null)]
    public void Verdict_WhenCounts_ShouldGiveChangePctAsAFraction(int[] counts, int closedAt, int lastEnded, double? change)
    {
        var result = RetroRules.Verdict(counts, closedAt, lastEnded);

        result.ChangePct.ShouldBe(change);
    }

    [TestCase(false, true, true, RetroCoverage.Done)]
    [TestCase(false, true, false, RetroCoverage.Missing)]
    [TestCase(true, false, true, RetroCoverage.Waiting)]
    [TestCase(true, false, false, RetroCoverage.Waiting)]
    [TestCase(true, true, true, RetroCoverage.Done)]
    [TestCase(true, true, false, RetroCoverage.Missing)]
    public void Coverage_WhenItem_ShouldBeDoneMissingOrWaiting(bool isFeature, bool lastChildCompleted, bool hasRetro, RetroCoverage expected)
    {
        RetroRules.Coverage(isFeature, lastChildCompleted, hasRetro).ShouldBe(expected);
    }
}
