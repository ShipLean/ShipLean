using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Journeys.Navigation;

// Picking another project in the side menu opens the same screen there, except where the screen belongs to the
// project you leave (a sprint's pages, a work item); the picker then lists the project you opened last first.
[TestFixture]
public class PickerJourneyTests : SeededAcceptanceTest
{
    [Test]
    public async Task J7_Picker_OpensSameScreenInOtherProject()
    {
        var picker = new PickerPicks(Page);

        // Stories board: same board.
        await new BoardScreen(Page).OpenAsync("SHIP", "stories");
        await Expect(Page.GetByTestId(PagesTestIds.BoardPage)).ToBeVisibleAsync();
        await picker.PickAsync("PAY");
        await Expect(Page).ToHaveURLAsync(PickerPicks.EndsWith(Links.Board("PAY", "stories")));
        await Expect(Page.GetByTestId(PagesTestIds.BoardPage)).ToBeVisibleAsync();
        await Expect(picker.Frame.Crumbs).ToHaveTextAsync(["PAY", "Boards", "Stories"]);

        // Backlogs in planning mode: same mode.
        await Page.GotoAsync(Links.Backlog("PAY", mode: "planning"));
        await Expect(Page.GetByTestId(PagesTestIds.BacklogPage)).ToBeVisibleAsync();
        await picker.PickAsync("MOB");
        await Expect(Page).ToHaveURLAsync(PickerPicks.EndsWith(Links.Backlog("MOB", mode: "planning")));
        await Expect(Page.GetByTestId(PagesTestIds.BacklogPage)).ToBeVisibleAsync();
        await Expect(picker.Frame.Crumbs).ToHaveTextAsync(["MOB", "Backlogs", "Stories", "Planning"]);

        // Features board: same level.
        await new BoardScreen(Page).OpenAsync("MOB", "features");
        await Expect(Page.GetByTestId(PagesTestIds.BoardPage)).ToBeVisibleAsync();
        await picker.PickAsync("DATA");
        await Expect(Page).ToHaveURLAsync(PickerPicks.EndsWith(Links.Board("DATA", "features")));
        await Expect(Page.GetByTestId(PagesTestIds.BoardPage)).ToBeVisibleAsync();
        await Expect(picker.Frame.Crumbs).ToHaveTextAsync(["DATA", "Boards", "Features"]);

        // Taskboard: the other project's All sprints (its sprint is not the other project's).
        await new SprintTaskboard(Page).OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        await Expect(Page.GetByTestId(PagesTestIds.TaskboardPage)).ToBeVisibleAsync();
        await picker.PickAsync("OPS");
        await Expect(Page).ToHaveURLAsync(PickerPicks.EndsWith(Links.Sprints("OPS")));
        await Expect(Page.GetByTestId(PagesTestIds.SprintsPage)).ToBeVisibleAsync();
        await Expect(picker.Frame.Crumbs).ToHaveTextAsync(["OPS", "Sprints", "All sprints"]);

        // Work item: the other project's Stories backlog.
        await new WorkItemScreen(Page).OpenAsync("SHIP", "SHIP-151");
        await Expect(Page.GetByTestId(PagesTestIds.WorkItemPage)).ToBeVisibleAsync();
        await picker.PickAsync("PAY");
        await Expect(Page).ToHaveURLAsync(PickerPicks.EndsWith(Links.Backlog("PAY")));
        await Expect(Page.GetByTestId(PagesTestIds.BacklogPage)).ToBeVisibleAsync();
        await Expect(picker.Frame.Crumbs).ToHaveTextAsync(["PAY", "Backlogs", "Stories"]);

        // The project opened last is first in the picker, and stays first after a reload.
        await picker.Frame.OpenPickerAsync();
        await Expect(picker.Frame.PickerOptions.Nth(0)).ToHaveTextAsync(new Regex("^PAY"));
        await Expect(picker.Frame.PickerOptions.Nth(1)).ToHaveTextAsync(new Regex("^SHIP"));
        await Page.ReloadAsync();
        await picker.Frame.OpenPickerAsync();
        await Expect(picker.Frame.PickerOptions.Nth(0)).ToHaveTextAsync(new Regex("^PAY"));
        await Expect(picker.Frame.PickerOptions.Nth(1)).ToHaveTextAsync(new Regex("^SHIP"));
    }
}
