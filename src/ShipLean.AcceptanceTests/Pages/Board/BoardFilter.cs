using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board;

// The board's filter bar and the note that says what it hides.
public sealed class BoardFilter(IPage page)
{
    public ILocator Toggle => page.GetByTestId(BoardTestIds.FilterToggle);

    public ILocator Keyword => page.GetByTestId(BoardTestIds.FilterKeyword);

    public ILocator Note => page.GetByTestId(BoardTestIds.FilterNote);

    public ILocator Clear => page.GetByTestId(BoardTestIds.FilterClear);

    // Opens the bar and types a keyword; the board is read again once typing pauses.
    public async Task FilterByKeywordAsync(string keyword)
    {
        await Toggle.ClickAsync();
        var read = await page.RunAndWaitForResponseAsync(
            () => Keyword.FillAsync(keyword),
            r => r.Request.Method == "GET" && r.Url.Contains("/api/boards/") && r.Url.Contains("q="));
        read.Ok.ShouldBeTrue();
    }
}
