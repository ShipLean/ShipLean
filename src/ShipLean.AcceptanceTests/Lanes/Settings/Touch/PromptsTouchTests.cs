using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Prompts;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Touch;

// SHIP-744 D14: the Prompts tab on a phone (390x844), as Joel (an admin). Reading and picking only; editing on a phone is not designed.
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
    public async Task Prompts_WhenOpenedOnAPhone_ShouldStackTheListAboveThePrompt()
    {
        var prompts = new PromptsMemberPage(Page);
        await prompts.OpenAsync("SHIP", "brief");
        await Expect(prompts.Rows).ToHaveCountAsync(11);

        var list = await prompts.List.BoundingBoxAsync();
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
        // The stage strip scrolls sideways inside itself by design; only its own overflow line is expected.
        (await TouchRules.ViolationsAsync(Page, prompts.StageStrip))
            .Where(line => !line.Contains("scrolls sideways", StringComparison.Ordinal))
            .ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, prompts.Detail)).ShouldBeEmpty();
    }

    [Test]
    public async Task Prompts_WhenTappedOnAPhone_ShouldOpenThePromptAndItsTabs()
    {
        var prompts = new PromptsMemberPage(Page);
        await prompts.OpenAsync("SHIP", "brief");

        await prompts.RowOf("guide.md").TapAsync();
        await Expect(prompts.DetailName).ToContainTextAsync("Planning and breakdown");
        await Page.GetByTestId(PromptsTestIds.TabHistory).TapAsync();
        await Expect(prompts.PanelHistory).ToBeVisibleAsync();
        await Page.GetByTestId(PromptsTestIds.TabAssembled).TapAsync();
        await Expect(prompts.Panel(PromptsTestIds.PanelAssembled)).ToBeVisibleAsync();
    }
}
