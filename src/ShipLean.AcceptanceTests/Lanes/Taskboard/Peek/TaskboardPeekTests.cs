using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.AcceptanceTests.Pages.Taskboard;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Taskboard.Peek;

// Sprint 24's Taskboard on the seeded Storefront project (SHIP), signed in as Joel.
[TestFixture]
public class TaskboardPeekTests : SeededAcceptanceTest
{
    [Test]
    public async Task TaskboardPeek_WhenTaskTitleClicked_ShouldOpenTaskOverTaskboard()
    {
        var board = new SprintTaskboard(Page);
        await board.OpenAsync("SHIP", SeedIds.Sprints.Sprint24);
        var title = board.TaskCard("SHIP-172").GetByTestId(TaskboardTestIds.CardTitle);
        var text = (await title.InnerTextAsync()).Trim();

        await title.ClickAsync();

        var peek = new ItemPeekDialog(Page);
        await peek.WaitOpenAsync("SHIP-172");
        await Expect(peek.Title).ToHaveValueAsync(text);
        await Expect(board.Grid).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"\?item=SHIP-172$"));

        await Page.GoBackAsync();

        await peek.WaitClosedAsync();
        await Expect(board.Grid).ToBeVisibleAsync();
        await Expect(Page).Not.ToHaveURLAsync(new Regex(@"item="));
    }
}
