using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Board;

// What a card shows of the board's style rules and tag colours.
public static class BoardStyles
{
    public static ILocator Tag(ILocator card, string name) =>
        card.GetByTestId(BoardTestIds.CardTag).Filter(new LocatorFilterOptions { HasTextRegex = new($"^{System.Text.RegularExpressions.Regex.Escape(name)}$") });
}
