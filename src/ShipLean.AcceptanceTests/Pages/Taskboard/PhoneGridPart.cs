using System.Text.RegularExpressions;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Taskboard;

// The shared Taskboard grid at 900px and below (SHIP-722): one task state a page in the touch pager, state chips above,
// lanes (stories, or people) on every page. The Feature board and the sprint Taskboard both draw it.
public sealed class PhoneGridPart(IPage page)
{
    public ILocator Grid => page.GetByTestId(TaskboardTestIds.PhoneGrid);

    public ILocator ChipStrip => Grid.GetByTestId(TouchTestIds.PagerChips);

    public ILocator Chips => Grid.GetByTestId(TouchTestIds.PagerChip);

    public ILocator Pages => Grid.GetByTestId(TouchTestIds.PagerPage);

    // A state's chip by the state's name (the chip also shows a count).
    public ILocator Chip(string stateName) =>
        Chips.Filter(new() { HasTextRegex = StartsWith(stateName) });

    // A state's page by the state's name.
    public ILocator PageNamed(string stateName) =>
        Pages.Filter(new() { Has = page.GetByTestId(TaskboardTestIds.PhonePageHead).Filter(new() { HasTextRegex = StartsWith(stateName) }) });

    // Waits for the first lane: the board's data has rendered.
    public Task WaitAsync() => Grid.GetByTestId(TaskboardTestIds.Lane).First.WaitForAsync();

    public static ILocator Lanes(ILocator statePage) => statePage.GetByTestId(TaskboardTestIds.Lane);

    public static ILocator ClosedLines(ILocator statePage) => statePage.GetByTestId(TaskboardTestIds.ClosedLine);

    // A lane on a page by its title (the story's title, or the person's name).
    public ILocator LaneTitled(ILocator statePage, string title) =>
        Lanes(statePage).Filter(new() { Has = page.GetByTestId(TaskboardTestIds.RowTitle).Filter(new() { HasTextRegex = new Regex("^\\s*" + Regex.Escape(title) + "\\s*$") }) });

    // The lanes' titles on a page, in order.
    public static Task<IReadOnlyList<string>> LaneTitlesAsync(ILocator statePage) =>
        Lanes(statePage).GetByTestId(TaskboardTestIds.RowTitle).AllInnerTextsAsync();

    // Taps a state's chip; the pager turns to that page.
    public Task TurnToAsync(string stateName) => Chip(stateName).TapAsync();

    // A page's header (its state and count): wholly in the viewport only while that page is on screen.
    public static ILocator PageHead(ILocator statePage) => statePage.GetByTestId(TaskboardTestIds.PhonePageHead);

    // A state's name at the start of a chip or page header, which go on with the count.
    private static Regex StartsWith(string stateName) => new("^\\s*" + Regex.Escape(stateName) + "(?![A-Za-z])");
}
