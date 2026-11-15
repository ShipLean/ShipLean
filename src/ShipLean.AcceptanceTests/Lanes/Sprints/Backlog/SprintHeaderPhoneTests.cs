using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// The shared sprint header on a phone (SHIP-724), on seeded Sprint 24 of SHIP. The goal test changes the sprint's goal;
// the others don't read it.
[TestFixture]
public class SprintHeaderPhoneTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task SprintHeader_WhenSprintPickedOnPhone_ShouldOpenSameTabOfThatSprint()
    {
        var header = new SprintHeaderPhone(Page);
        await header.OpenAsync(Links.SprintCapacity("SHIP", SeedIds.Sprints.Sprint24));
        (await TouchRules.ViolationsAsync(Page, header.Header)).ShouldBeEmpty();

        await header.PickAsync("Sprint 23");

        await Expect(header.Picker).ToContainTextAsync("Sprint 23");
        new Uri(Page.Url).AbsolutePath.ShouldBe(Links.SprintCapacity("SHIP", SeedIds.Sprints.Sprint23));
        await Expect(new TouchShell(Page).Sheets).ToHaveCountAsync(0);
    }

    [Test]
    public async Task SprintHeader_WhenGoalEditedOnPhone_ShouldSaveFromSaveBar()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        var header = new SprintHeaderPhone(Page);
        var shell = new TouchShell(Page);
        await header.OpenAsync(Links.SprintBacklog("SHIP", sprint));

        await header.MoreActionAsync("Edit goal");
        await header.GoalInput.FillAsync("Ship saved cards and passkeys");
        await Expect(shell.SaveBar).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, header.Header)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, shell.SaveBar)).ShouldBeEmpty();
        var saved = await header.SaveGoalAsync(sprint);
        saved.Ok.ShouldBeTrue();

        await Expect(header.Goal).ToHaveTextAsync("Ship saved cards and passkeys");
        await Expect(header.GoalInput).ToHaveCountAsync(0);
        await Expect(shell.SaveBar).ToHaveCountAsync(0);
        await Page.ReloadAsync();
        await Expect(header.Goal).ToHaveTextAsync("Ship saved cards and passkeys");
    }

    [Test]
    public async Task SprintHeader_WhenTaskboardOpenedOnPhone_ShouldShowGoalAndTabs()
    {
        var header = new SprintHeaderPhone(Page);

        await header.OpenAsync(Links.Taskboard("SHIP", SeedIds.Sprints.Sprint24));

        await Expect(header.Goal).ToBeVisibleAsync();
        await Expect(header.Tabs).ToBeVisibleAsync();
        await Expect(header.Tabs.GetByTestId(SprintHeaderTestIds.TabTaskboard)).ToHaveAttributeAsync("aria-current", "page");
        await Expect(header.More).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, header.Header)).ShouldBeEmpty();
    }
}
