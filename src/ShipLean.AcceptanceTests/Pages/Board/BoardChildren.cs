using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board;

// The child list a card's roll-up opens.
public sealed class BoardChildren(IPage page)
{
    public ILocator List => page.GetByTestId(BoardTestIds.ChildList);

    public ILocator Rows => List.GetByTestId(BoardTestIds.ChildRow);

    public ILocator Row(string key) =>
        Rows.Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"{Regex.Escape(key)}(?!\\d)") });

    // The roll-up of one child type on a card: the type icon's letter (S, B, F) then closed/total, as in "S1/3".
    public static ILocator Rollup(ILocator card, string typeLetter) =>
        card.GetByTestId(BoardTestIds.CardRollup).Filter(new LocatorFilterOptions { HasTextRegex = new Regex($"^{typeLetter}\\d") });

    // The "+ User story" / "+ Bug" button, by its label.
    public ILocator Add(string label) =>
        List.GetByTestId(BoardTestIds.ChildAdd).Filter(new LocatorFilterOptions { HasText = label });

    public async Task OpenAsync(ILocator card, string type)
    {
        var read = await page.RunAndWaitForResponseAsync(
            () => Rollup(card, type).ClickAsync(),
            r => r.Request.Method == "GET" && r.Url.Contains("/children"));
        read.Ok.ShouldBeTrue();
    }
}
