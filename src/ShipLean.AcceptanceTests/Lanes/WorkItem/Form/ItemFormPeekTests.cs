using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.ItemPeek;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Form;

// Links inside the item form open the item in the modal. Reads only.
[TestFixture]
public class ItemFormPeekTests : SeededAcceptanceTest
{
    [Test]
    public async Task ItemFormPeek_WhenTaskClickedOnItemPage_ShouldOpenTaskOverPage()
    {
        var screen = new WorkItemScreen(Page);
        var peek = new ItemPeekDialog(Page);
        await screen.OpenAsync("SHIP", "SHIP-141");

        await Page.GetByTestId(WorkItemTestIds.TaskOpen)
            .Filter(new LocatorFilterOptions { HasTextString = "UI: saved card picker" })
            .ClickAsync();

        await peek.WaitOpenAsync("SHIP-172");
        await Expect(peek.Title).ToHaveValueAsync("UI: saved card picker");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/p/SHIP/items/SHIP-141\?item=SHIP-172$"));

        await Page.GoBackAsync();

        await peek.WaitClosedAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(@"/p/SHIP/items/SHIP-141$"));
        await Expect(screen.Title).ToHaveValueAsync("Pay with saved card");
    }

    [Test]
    public async Task ItemFormPeek_WhenParentOpenedInsideModal_ShouldSwapToParent()
    {
        var peek = new ItemPeekDialog(Page);
        await Page.GotoAsync(Links.WithItem(Links.Board("SHIP", "stories"), "SHIP-172"));
        await peek.WaitOpenAsync("SHIP-172");

        await peek.Dialog.GetByTestId(WorkItemTestIds.ParentOpen).ClickAsync();

        await peek.WaitOpenAsync("SHIP-141");
        await Expect(peek.Title).ToHaveValueAsync("Pay with saved card");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/p/SHIP/boards/stories\?item=SHIP-141$"));

        await Page.GoBackAsync();

        await peek.WaitOpenAsync("SHIP-172");
        await Expect(peek.Title).ToHaveValueAsync("UI: saved card picker");
    }
}
