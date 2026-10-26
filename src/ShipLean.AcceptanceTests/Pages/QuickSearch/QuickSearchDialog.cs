using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.QuickSearch;

// The quick search dialog and the two ways to open it.
public sealed class QuickSearchDialog(IPage page)
{
    public const string Shortcut = "ControlOrMeta+P";

    public ILocator Dialog => page.GetByTestId(QuickSearchTestIds.Dialog);

    public ILocator Input => page.GetByTestId(QuickSearchTestIds.Input);

    public ILocator Options => page.GetByTestId(QuickSearchTestIds.Option);

    public ILocator Groups => page.GetByTestId(QuickSearchTestIds.Group);

    public ILocator Error => page.GetByTestId(QuickSearchTestIds.Error);

    public ILocator Note => page.GetByTestId(QuickSearchTestIds.Note);

    public ILocator SearchButton => page.GetByTestId(ShellTestIds.SearchButton);

    public ILocator SearchIconButton => page.GetByTestId(ShellTestIds.SearchIconButton);

    // A row is found by the exact text of its key (items, projects) or its label (sprints' names, actions' labels).
    public ILocator Item(string key) => ByText(QuickSearchTestIds.OptionKey, key);

    public ILocator Project(string key) => ByText(QuickSearchTestIds.OptionKey, key);

    public ILocator Sprint(string name) => ByText(QuickSearchTestIds.OptionLabel, name);

    public ILocator Action(string label) => ByText(QuickSearchTestIds.OptionLabel, label);

    public async Task OpenWithShortcutAsync()
    {
        await page.Keyboard.PressAsync(Shortcut);
        await Input.WaitForAsync();
    }

    public async Task OpenWithButtonAsync()
    {
        await SearchButton.ClickAsync();
        await Input.WaitForAsync();
    }

    public async Task OpenWithIconButtonAsync()
    {
        await SearchIconButton.ClickAsync();
        await Input.WaitForAsync();
    }

    // Types the query and returns the /api/search response it causes.
    public Task<IResponse> TypeAsync(string query) =>
        page.RunAndWaitForApiAsync("/api/search", "GET", () => Input.FillAsync(query));

    public Task RetryAsync() => page.GetByTestId(QuickSearchTestIds.Retry).ClickAsync();

    private ILocator ByText(string testId, string text) =>
        Options.Filter(new() { Has = page.GetByTestId(testId).Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(text)}$") }) });
}
