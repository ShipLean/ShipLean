using Microsoft.Playwright;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Touch;

// SHIP-744 D14: Ways of working on a phone (390x844), as Joel (an admin). Reading and picking only; editing on a phone is not designed.
[TestFixture]
public class PromptsTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [OneTimeSetUp]
    public async Task ArrangeACustomBriefAsync()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief)
            .WithText("# Brief\n\nBuild {{key}} the way the team does.")
            .WithNote("Team wording")
            .WithCreatedBy(SeedIds.Users.Joel)
            .InUse()
            .InsertAsync(ConnectionString);
    }

    [Test]
    public async Task Prompts_WhenOpenedOnAPhone_ShouldStackTheStageNavAboveThePrompt()
    {
        var prompts = new PromptsMemberPage(Page);
        await prompts.OpenAsync("SHIP", "brief");
        await Expect(prompts.StageCounts).ToHaveTextAsync(PromptsMemberPage.TwelvePromptsByStage);

        var list = await prompts.StageNav.BoundingBoxAsync();
        var detail = await prompts.Detail.BoundingBoxAsync();
        list.ShouldNotBeNull();
        detail.ShouldNotBeNull();
        (list.Y + list.Height).ShouldBeLessThanOrEqualTo(detail.Y + 1);
    }

    [Test]
    public async Task Prompts_WhenOnAPhone_ShouldPassTheTouchRules()
    {
        var prompts = new PromptsMemberPage(Page);
        await prompts.OpenAsync("SHIP", "brief");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");

        (await TouchRules.ViolationsAsync(Page)).ShouldBeEmpty();
        // Under 900px the strip gives way to a stage select and the chips wrap: nothing scrolls sideways (D7).
        await Expect(prompts.StageSelect).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, prompts.StageNav)).ShouldBeEmpty();
        await Expect(prompts.Panel(PromptsTestIds.PanelSource)).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, prompts.Detail)).ShouldBeEmpty();
    }

    [Test]
    public async Task Prompts_WhenTappedOnAPhone_ShouldOpenThePromptAndItsTabs()
    {
        var prompts = new PromptsMemberPage(Page);
        await prompts.OpenAsync("SHIP", "brief");

        await Page.GetByTestId(PromptsTestIds.TabHistory).TapAsync();
        await Expect(prompts.PanelHistory).ToBeVisibleAsync();
        await prompts.StageSelect.SelectOptionAsync(new SelectOptionValue { Label = "3 · Plan · 2 prompts" });
        await Expect(prompts.DetailName).ToContainTextAsync("Planning and breakdown");
        await Expect(Page.GetByTestId(PromptsTestIds.TabHistory)).ToHaveCountAsync(0);
        await Page.GetByTestId(PromptsTestIds.TabAssembled).TapAsync();
        await Expect(prompts.Panel(PromptsTestIds.PanelAssembled)).ToBeVisibleAsync();
    }
}
