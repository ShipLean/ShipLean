using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Board;
using ShipLean.AcceptanceTests.Pages.Board.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Board.Touch;

// Assign (Owner on Features) from a card's ⋯ sheet, and the children list, on a phone (SHIP-719 L719e). The touch rules
// are checked on this task's regions only: the Assign / Owner sheet and the children list.
[TestFixture]
public class BoardAssignTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    private async Task<BoardScreen> OpenAsync(string level)
    {
        var board = new BoardScreen(Page);
        await board.OpenAsync("SHIP", level);
        await new BoardPager(Page).WaitAsync();
        return board;
    }

    private async Task<string?> AssigneeNameAsync(string key)
    {
        var response = await Page.Context.APIRequest.GetAsync($"{BaseUrl}/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/{key}");
        response.Ok.ShouldBeTrue(await response.TextAsync());
        var assignee = JsonDocument.Parse(await response.TextAsync()).RootElement.GetProperty("assignee");
        return assignee.ValueKind == JsonValueKind.Null ? null : assignee.GetProperty("name").GetString();
    }

    [Test]
    public async Task Card_WhenAssignedFromSheetOnPhone_ShouldShowAssignee()
    {
        var board = await OpenAsync("stories");
        var sheets = new CardActionsSheet(Page);
        var card = board.Card("SHIP-144");

        await sheets.OpenAssignAsync(card, "SHIP-144", "Assign");
        var sheet = sheets.AssignSheet("Assign", "SHIP-144");
        await Expect(sheet).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, sheet)).ShouldBeEmpty();
        var patched = await Page.RunAndWaitForResponseAsync(
            () => CardActionsSheet.Person(sheet, "Tom Okafor").TapAsync(),
            r => r.Request.Method == "PATCH" && new Uri(r.Url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal));

        patched.Ok.ShouldBeTrue(await patched.TextAsync());
        await Expect(BoardMoves.Assignee(card).First).ToHaveAttributeAsync("title", "Tom Okafor");
        (await AssigneeNameAsync("SHIP-144")).ShouldBe("Tom Okafor");
    }

    [Test]
    public async Task Feature_WhenOwnerClearedOnPhone_ShouldShowNoOwner()
    {
        var board = await OpenAsync("features");
        var sheets = new CardActionsSheet(Page);
        var card = board.Card("SHIP-104");
        await Expect(BoardMoves.Assignee(card).First).ToHaveAttributeAsync("title", "Tom Okafor");

        await sheets.OpenAssignAsync(card, "SHIP-104", "Owner");
        var sheet = sheets.AssignSheet("Owner", "SHIP-104");
        await Expect(sheet).ToBeVisibleAsync();
        (await TouchRules.ViolationsAsync(Page, sheet)).ShouldBeEmpty();
        var patched = await Page.RunAndWaitForResponseAsync(
            () => CardActionsSheet.Person(sheet, "No owner").TapAsync(),
            r => r.Request.Method == "PATCH" && new Uri(r.Url).AbsolutePath.StartsWith("/api/work-items/", StringComparison.Ordinal));

        patched.Ok.ShouldBeTrue(await patched.TextAsync());
        await Expect(BoardMoves.Assignee(card).First).ToHaveAttributeAsync("data-variant", "none");
        (await AssigneeNameAsync("SHIP-104")).ShouldBeNull();
    }
}
