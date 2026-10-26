using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Sprints;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Sprints.Backlog;

// Editing the current sprint's goal from its header, in a class of its own: it changes the sprint.
[TestFixture]
public class SprintGoalTests : SeededAcceptanceTest
{
    [Test]
    public async Task SprintHeader_WhenGoalEdited_ShouldSave()
    {
        var sprint = SeedIds.Sprints.Sprint24;
        var backlog = new SprintBacklogPage(Page);
        await backlog.OpenAsync("SHIP", sprint);

        await backlog.GoalEdit.ClickAsync();
        await backlog.GoalInput.FillAsync("Ship saved cards and passkeys");
        var saved = await backlog.SaveGoalAsync(sprint);
        saved.Ok.ShouldBeTrue();

        await Expect(backlog.Goal).ToHaveTextAsync("Ship saved cards and passkeys");
        await Expect(backlog.GoalInput).ToHaveCountAsync(0);
        await Page.ReloadAsync();
        await Expect(backlog.Goal).ToHaveTextAsync("Ship saved cards and passkeys");
    }
}
