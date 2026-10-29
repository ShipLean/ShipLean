using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Levels;

// The Features and Epics backlogs on the seeded Storefront project (SHIP), signed in as Joel. Nothing here changes data.
[TestFixture]
public class LevelBacklogTests : SeededAcceptanceTest
{
    [Test]
    public async Task Features_WhenNestedFeature_ShouldRollUpProgress()
    {
        var backlog = new LevelBacklog(Page);
        await backlog.OpenAsync("SHIP", "features");

        // Reporting (SHIP-103) counts the stories under its nested Feature, Dashboard widgets (SHIP-108): 1 of 5 done.
        await Expect(backlog.ProgressText("SHIP-103")).ToHaveTextAsync("1 / 5 done");
        await Expect(backlog.ProgressText("SHIP-108")).ToHaveTextAsync("0 / 2 done");
        await Expect(backlog.ProgressText("SHIP-101")).ToHaveTextAsync("4 / 8 done");
    }

    [Test]
    public async Task Epics_WhenListed_ShouldCountNestedFeatures()
    {
        var backlog = new LevelBacklog(Page);
        await backlog.OpenAsync("SHIP", "epics");

        // Insights for merchants (SHIP-92) has Reporting and, inside it, Dashboard widgets.
        await Expect(backlog.FeatureCount("SHIP-92")).ToHaveTextAsync("2");
        await Expect(backlog.FeatureCount("SHIP-90")).ToHaveTextAsync("1");
        await Expect(backlog.Level("Epics")).ToHaveAttributeAsync("aria-current", "page");
    }

    [Test]
    public async Task Features_WhenSumExceedsOwnBy25Percent_ShouldWarn()
    {
        var backlog = new LevelBacklog(Page);
        await backlog.OpenAsync("SHIP", "features");

        // 26 points under a 20-point Feature is over by 30%; 32 under a 30-point Epic is not.
        await Expect(backlog.PointsSum("SHIP-101")).ToHaveTextAsync("· Σ26");
        await Expect(backlog.PointsSum("SHIP-101")).ToHaveAttributeAsync("data-warn", "true");

        await backlog.Level("Epics").ClickAsync();
        await Expect(backlog.PointsSum("SHIP-91")).ToHaveTextAsync("· Σ32");
        await Expect(backlog.PointsSum("SHIP-91")).ToHaveAttributeAsync("data-warn", "false");
    }
}
