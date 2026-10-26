using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.QuickSearch;

// The quick search dialog and the two ways to open it. Action rows are found by data-id (create-bug, toggle-theme, ...),
// items and projects by data-key, and sprints by data-id (their address).
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

    // One row: kind is item, sprint, project or action; keyOrId is an item's or project's key, a sprint's address or an
    // action's id.
    public ILocator Option(string kind, string keyOrId)
    {
        var attribute = kind is "item" or "project" ? "data-key" : "data-id";
        // The contract (K6) names rows by data-key and data-id, which GetByTestId cannot select. The analyzers flag
        // Locator and XPath for that reason, so this one place opts out.
#pragma warning disable AG0045, AG0046
        return Options.Locator($"xpath=self::*[@data-kind='{kind}' and @{attribute}='{keyOrId}']");
#pragma warning restore AG0045, AG0046
    }

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

    public Task PickAsync(string kind, string keyOrId) => Option(kind, keyOrId).ClickAsync();

    public Task RetryAsync() => page.GetByTestId(QuickSearchTestIds.Retry).ClickAsync();
}
