using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Routes;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Shell.Routes;

// The route table against the real server: a sprint page resolves the project and sprint it names, and a project the
// caller is not a member of is not found.
[TestFixture]
public class RoutesTests : SeededAcceptanceTest
{
    [Test]
    public async Task Routes_WhenSprintBacklogOpened_ShouldShowTheSprintsSlots()
    {
        var screen = new ScaffoldScreen(Page);

        await screen.OpenAsync(ScaffoldScreen.SprintBacklogPath("SHIP", SeedIds.Sprints.Sprint24));

        await Expect(screen.SprintBacklogPage).ToBeVisibleAsync();
        await Expect(screen.SprintBacklogSlot).ToBeVisibleAsync();
        await Expect(screen.NotFound).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Routes_WhenOtherProjectsKey_ShouldShowNotFound()
    {
        var screen = new ScaffoldScreen(Page);

        await screen.OpenAsync(ScaffoldScreen.BacklogPath("PART"));

        await Expect(screen.NotFound).ToBeVisibleAsync();
    }
}
