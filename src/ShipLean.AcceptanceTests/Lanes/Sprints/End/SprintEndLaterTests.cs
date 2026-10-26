using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using static ShipLean.AcceptanceTests.Lanes.Sprints.End.EndedSprint;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.End;

// Sprint 24 ended and undecided. Decide later changes nothing on the server, so the tests share one arrangement.
[TestFixture]
public class SprintEndLaterTests : SeededAcceptanceTest
{
    [Test]
    public async Task SprintEnd_WhenDecideLater_ShouldShowBannerWithReview()
    {
        await EndSprint24Async(ConnectionString);
        var keys = Keys(await ReadRolloverAsync(Page, ShipLean.TestSupport.SeedIds.Sprints.Sprint24));
        var prompt = new SprintEndPrompt(Page);
        await prompt.OpenAsync("SHIP");
        await Expect(prompt.Rows).ToHaveCountAsync(keys.Count);

        await prompt.Later.ClickAsync();

        await Expect(prompt.Dialog).ToHaveCountAsync(0);
        await Expect(prompt.Banner).ToContainTextAsync($"Sprint 24 has {keys.Count} unfinished");

        // Still there after a reload, as a banner and not the dialog.
        await prompt.OpenAsync("SHIP");
        await Expect(prompt.Banner).ToBeVisibleAsync();
        await Expect(prompt.Dialog).ToHaveCountAsync(0);

        await prompt.Review.ClickAsync();

        await Expect(prompt.Dialog).ToBeVisibleAsync();
        await Expect(prompt.Rows).ToHaveCountAsync(keys.Count);
        await Expect(prompt.Banner).ToHaveCountAsync(0);
    }
}
