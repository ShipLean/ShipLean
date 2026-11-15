using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// The sprint Retro tab's touch parts (SHIP-727): the four numbers, the toolbar's Filter and ⋯, the category tiles and
// the bottom sheet a number opens. Use with SprintRetroPage for the panel, board and notes.
public sealed class SprintRetroPhone(IPage page)
{
    public ILocator Numbers => page.GetByTestId(RetrosTestIds.RetroNumbers);

    public ILocator NumberRetros => page.GetByTestId(RetrosTestIds.RetroNumberRetros);

    public ILocator NumberRecurring => page.GetByTestId(RetrosTestIds.RetroNumberRecurring);

    public ILocator NumberToDecide => page.GetByTestId(RetrosTestIds.RetroNumberToDecide);

    public ILocator NumberActions => page.GetByTestId(RetrosTestIds.RetroNumberActions);

    public ILocator Toolbar => page.GetByTestId(RetrosTestIds.RetroToolbar);

    public ILocator FilterOpen => page.GetByTestId(RetrosTestIds.RetroFilterOpen);

    public ILocator Tiles => page.GetByTestId(RetrosTestIds.RetroCategoryTiles);

    public ILocator AllTiles => page.GetByTestId(RetrosTestIds.RetroCategoryTile);

    // A tile by its label ("Went badly"); its data-category is the address value.
    public ILocator Tile(string label) => AllTiles.Filter(new() { HasTextRegex = new Regex($"^{Regex.Escape(label)}") });

    public ILocator Sheet => page.GetByTestId(TouchTestIds.Sheet);

    public ILocator SheetTitle => page.GetByTestId(TouchTestIds.SheetTitle);
}
