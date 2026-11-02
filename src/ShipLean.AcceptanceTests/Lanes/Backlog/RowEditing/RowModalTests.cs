using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.RowEditing;

// Opening a row in the item modal over the Stories list, and what saving there leaves behind.
[TestFixture]
public class RowModalTests : SeededAcceptanceTest
{
    [Test]
    public async Task Modal_WhenSavedOnBacklog_ShouldLeaveTheNextBulkWriteFresh()
    {
        var backlog = new StoriesBacklog(Page);
        var editing = new BacklogRowEditing(Page);
        var peek = new ItemPeekDialog(Page);
        var form = new WorkItemScreen(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var key = (await editing.KeysAsync())[2];

        await editing.Title(key).ClickAsync();
        await peek.WaitOpenAsync(key);
        await form.Points.FillAsync("40");
        var saved = await form.SaveAsync(await IdOfAsync(editing, key));
        saved.Ok.ShouldBeTrue(await saved.TextAsync());
        await peek.Close.ClickAsync();
        await peek.WaitClosedAsync();

        // The row behind the modal has the saved estimate, with no reload.
        await Expect(editing.PointsCell(key)).ToHaveTextAsync("40");

        await editing.ToggleSelectAsync(key);
        await Expect(editing.Row(key)).ToHaveAttributeAsync("data-selected", "true");
        var sprint = await editing.Row(key).GetByTestId(BacklogTestIds.SprintChip).TextContentAsync();
        var bulk = await Page.RunAndWaitForResponseAsync(
            () => editing.BulkMoveSprint.Filter(new() { HasNotTextString = sprint }).First.ClickAsync(),
            r => r.Request.Method == "POST" && r.Url.EndsWith("/backlog/bulk", StringComparison.Ordinal));

        bulk.Ok.ShouldBeTrue(await bulk.TextAsync());
        var body = System.Text.Json.JsonDocument.Parse(await bulk.TextAsync()).RootElement;
        body.GetProperty("stale").GetArrayLength().ShouldBe(0);
        body.GetProperty("written").GetArrayLength().ShouldBe(1);
        await Expect(editing.BulkChanged).ToHaveCountAsync(0);
        body.GetProperty("written")[0].GetProperty("id").GetGuid().ShouldBe(await IdOfAsync(editing, key));
    }

    [Test]
    public async Task Row_WhenTitleClickedOrEnterPressed_ShouldOpenModal()
    {
        var backlog = new StoriesBacklog(Page);
        var editing = new BacklogRowEditing(Page);
        var peek = new ItemPeekDialog(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var keys = (await editing.KeysAsync()).Take(2).ToList();

        // A click elsewhere on the row only moves focus.
        await editing.KeyCell(keys[0]).ClickAsync();
        await Expect(editing.Row(keys[0])).ToHaveAttributeAsync("data-focused", "true");
        await Expect(editing.Dialog).ToHaveCountAsync(0);

        // Enter opens the focused row.
        await Page.Keyboard.PressAsync("Enter");
        await peek.WaitOpenAsync(keys[0]);
        await peek.Close.ClickAsync();
        await peek.WaitClosedAsync();

        // A plain click on the title opens it.
        await editing.Title(keys[1]).ClickAsync();
        await peek.WaitOpenAsync(keys[1]);
        await Expect(Page).ToHaveURLAsync(new Regex($@"\?item={keys[1]}$"));
    }

    [Test]
    public async Task Row_WhenCtrlClickedWithSelection_ShouldSelectInsteadOfOpening()
    {
        var backlog = new StoriesBacklog(Page);
        var editing = new BacklogRowEditing(Page);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var keys = (await editing.KeysAsync()).Take(2).ToList();

        await editing.ToggleSelectAsync(keys[0]);
        await Expect(editing.BulkBar).ToBeVisibleAsync();

        await editing.Title(keys[1]).ClickAsync(new() { Modifiers = [KeyboardModifier.Control] });

        await Expect(editing.Row(keys[1])).ToHaveAttributeAsync("data-selected", "true");
        await Expect(editing.Dialog).ToHaveCountAsync(0);
        await Expect(Page).ToHaveURLAsync(new Regex(@"/p/SHIP/backlog[^?]*$"));
    }

    [TestCase(1280, 800)]
    [TestCase(1440, 900)]
    public async Task Layout_WhenFieldAndModalOverBacklog_ShouldNotOverflow(int width, int height)
    {
        var backlog = new StoriesBacklog(Page);
        var editing = new BacklogRowEditing(Page);
        var peek = new ItemPeekDialog(Page);
        await Page.SetViewportSizeAsync(width, height);
        await backlog.OpenAsync("SHIP");
        await Expect(backlog.Rows.First).ToBeVisibleAsync();
        var key = (await editing.KeysAsync())[0];
        (await editing.OverflowsSidewaysAsync()).ShouldBeFalse("the backlog on its own");

        await editing.PointsCell(key).ClickAsync();
        await Expect(editing.PointsField).ToBeVisibleAsync();
        (await editing.FitsViewportAsync(editing.PointsField)).ShouldBeTrue("the points field");
        (await editing.OverflowsSidewaysAsync()).ShouldBeFalse("the backlog with the points field open");
        await editing.PointsField.PressAsync("Escape");

        await editing.Title(key).ClickAsync();
        await peek.WaitOpenAsync(key);
        (await editing.FitsViewportAsync(peek.Dialog)).ShouldBeTrue("the modal");
        (await editing.OverflowsSidewaysAsync()).ShouldBeFalse("the backlog with the modal open");
    }

    private static async Task<Guid> IdOfAsync(BacklogRowEditing editing, string key)
    {
        var id = await editing.Row(key).GetAttributeAsync("data-row-id");
        return Guid.Parse(id!);
    }
}
