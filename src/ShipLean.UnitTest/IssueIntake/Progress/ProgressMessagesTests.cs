using ShipLean.Application.Features.IssueIntake.Progress;

namespace ShipLean.UnitTest.IssueIntake.Progress;

// D13, D14, D17: the exact progress texts. They name only keys, titles, sprint names and dates, and public pull request links.
[TestFixture]
public class ProgressMessagesTests
{
    [Test]
    public void Sprint_WhenPlanned_ShouldNameSprintAndDates()
    {
        ProgressMessages.Sprint("SHIP-12", "Fix the login label", "Sprint 4", "2026-11-10", "2026-11-21")
            .ShouldBe("SHIP-12 \"Fix the login label\" is planned for the sprint Sprint 4 (2026-11-10 to 2026-11-21).");
    }

    [Test]
    public void Started_WhenInProgress_ShouldSayStarted()
    {
        ProgressMessages.Started("SHIP-12", "Fix the login label").ShouldBe("Work on SHIP-12 \"Fix the login label\" has started.");
    }

    [Test]
    public void PullRequest_WhenOpenOrMerged_ShouldLinkPullRequest()
    {
        ProgressMessages.PullRequest("SHIP-12", "Fix", "https://github.com/acme/app/pull/7", merged: false)
            .ShouldBe("A pull request for SHIP-12 \"Fix\" is open: https://github.com/acme/app/pull/7");
        ProgressMessages.PullRequest("SHIP-12", "Fix", "https://github.com/acme/app/pull/7", merged: true)
            .ShouldBe("A pull request for SHIP-12 \"Fix\" was merged: https://github.com/acme/app/pull/7");
    }

    [Test]
    public void ChildDone_WhenChildCompleted_ShouldCount()
    {
        ProgressMessages.ChildDone("SHIP-10", "Search", "SHIP-11", "Index titles", 2, 5)
            .ShouldBe("SHIP-11 \"Index titles\" is done: 2 of 5 done for SHIP-10 \"Search\".");
    }

    [Test]
    public void Done_WhenComposed_ShouldPutSectionBeforeClosingLine()
    {
        ProgressMessages.Done("SHIP-12", "Fix", "### Shipped in\n\nShipLean couldn't confirm the version this shipped in.")
            .ShouldBe("SHIP-12 \"Fix\" is done.\n\n### Shipped in\n\nShipLean couldn't confirm the version this shipped in.\n\nShipLean is closing this issue as completed.");
    }

    [Test]
    public void Removed_WhenRemoved_ShouldSayNotPlanned()
    {
        ProgressMessages.Removed("SHIP-12", "Fix").ShouldBe("SHIP-12 \"Fix\" was removed from the plan. ShipLean is closing this issue as not planned.");
    }

    [Test]
    public void Started_WhenTitleHasLineBreaks_ShouldKeepOneLine()
    {
        ProgressMessages.Started("SHIP-12", "Fix\n# the\r\nlabel").ShouldBe("Work on SHIP-12 \"Fix # the label\" has started.");
    }
}
