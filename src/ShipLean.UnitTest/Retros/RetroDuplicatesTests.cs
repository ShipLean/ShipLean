using ShipLean.Application.Features.Retros.Decide;

namespace ShipLean.UnitTest.Retros;

[TestFixture]
public class RetroDuplicatesTests
{
    private static DecideOpenAction Open(string title, string key = "SHIP-1") => new(Guid.NewGuid(), key, title, "New");

    [Test]
    public void Find_WhenSameWordsInAnotherOrder_ShouldMatch()
    {
        var open = Open("Retry the flaky browser tests");

        var found = RetroDuplicates.Find("Flaky browser tests: retry them", [open]);

        found.ShouldBe([open]);
    }

    [Test]
    public void Find_WhenHalfOfTheSmallerSetIsShared_ShouldMatch()
    {
        var open = Open("Document deployment rollback steps");

        // {document, deployment, rollback, steps} against {deployment, rollback}: both of the smaller set are shared.
        RetroDuplicates.Find("Deployment rollback", [open]).ShouldBe([open]);
        // {document, deployment, rollback, steps} against {deployment, ownership, handover, checklist}: one of four is not half of four.
        RetroDuplicates.Find("Deployment ownership handover checklist", [open]).ShouldBeEmpty();
    }

    [Test]
    public void Find_WhenExactlyHalfOfTheSmallerSetIsShared_ShouldMatch()
    {
        var open = Open("Alpha bravo charlie delta");

        RetroDuplicates.Find("Alpha bravo xylophone zebra", [open]).ShouldBe([open]);
        RetroDuplicates.Find("Alpha xylophone zebra quartz", [open]).ShouldBeEmpty();
    }

    [Test]
    public void Find_ShouldIgnoreWordsOfThreeLettersOrFewer()
    {
        var open = Open("Fix the CI job for all PRs");

        // Everything shared is a short word, and nothing else is left to compare.
        RetroDuplicates.Find("Fix the CI job for all PRs", [open]).ShouldBeEmpty();
    }

    [Test]
    public void Find_ShouldIgnoreCaseAndPunctuation()
    {
        var open = Open("SKILL: Update Planning-Guide");

        RetroDuplicates.Find("update the planning guide skill", [open]).ShouldBe([open]);
    }

    [Test]
    public void Find_WhenNothingIsShared_ShouldBeEmpty()
    {
        RetroDuplicates.Find("Improve onboarding documentation", [Open("Reduce flaky browser tests")]).ShouldBeEmpty();
    }

    [Test]
    public void Find_WhenTitleHasNoLongWords_ShouldBeEmpty()
    {
        RetroDuplicates.Find("Fix it", [Open("Fix it")]).ShouldBeEmpty();
    }

    [Test]
    public void Find_ShouldReturnEveryMatchInOrder()
    {
        var first = Open("Stabilise browser tests", "SHIP-1");
        var other = Open("Unrelated planning chore", "SHIP-2");
        var last = Open("Browser tests stabilisation work", "SHIP-3");

        RetroDuplicates.Find("Stabilise browser tests", [first, other, last]).ShouldBe([first, last]);
    }
}
