using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.QuickSearch;
using ShipLean.AcceptanceTests.Pages.Shell;

namespace ShipLean.AcceptanceTests.Lanes.Shell.QuickSearch;

// The ways to open quick search against the real server, signed in as Joel.
[TestFixture]
public class QuickSearchShellTests : SeededAcceptanceTest
{
    [Test]
    public async Task QuickSearch_WhenShortcutPressed_ShouldOpenAndCloseOnEscape()
    {
        var search = new QuickSearchDialog(Page);
        await new ShellFrame(Page).OpenAsync(ShellFrame.BacklogPath("SHIP"));

        await search.OpenWithShortcutAsync();

        await Expect(search.Dialog).ToBeVisibleAsync();
        await Expect(search.Input).ToBeFocusedAsync();
        // The old shortcut opened the project picker. Catches a shortcut that still opens the picker (or both).
        await Expect(new ShellFrame(Page).Picker).ToHaveCountAsync(0);
        await Page.Keyboard.PressAsync("Escape");
        await Expect(search.Dialog).ToHaveCountAsync(0);
    }

    [Test]
    public async Task QuickSearch_WhenSearchButtonClicked_ShouldOpen()
    {
        var search = new QuickSearchDialog(Page);
        await new ShellFrame(Page).OpenAsync(ShellFrame.BacklogPath("SHIP"));

        await search.OpenWithButtonAsync();

        await Expect(search.Dialog).ToBeVisibleAsync();
        await Expect(search.Input).ToBeFocusedAsync();
        // With nothing typed the dialog lists the actions. Catches a dialog that opens empty.
        await Expect(search.Option("action", "create-bug")).ToBeVisibleAsync();
    }

    [Test]
    public async Task QuickSearch_WhenNarrow_ShouldOpenFromTheStrip()
    {
        var search = new QuickSearchDialog(Page);
        await Page.SetViewportSizeAsync(800, 700);
        await new ShellFrame(Page).OpenAsync(ShellFrame.BacklogPath("SHIP"));

        // Catches a strip that keeps the wide button, which would not fit.
        await Expect(search.SearchButton).ToBeHiddenAsync();
        await search.OpenWithIconButtonAsync();

        await Expect(search.Dialog).ToBeVisibleAsync();
    }
}
