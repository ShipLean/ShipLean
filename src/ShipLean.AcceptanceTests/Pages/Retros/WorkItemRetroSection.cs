using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// The read-only Retrospective section on a work item's form: that item's retros as the By work item cards.
public sealed class WorkItemRetroSection(IPage page)
{
    public ILocator Section => page.GetByTestId(RetrosTestIds.WorkItemRetroSection);

    public ILocator Cards => Section.GetByTestId(RetroSharedTestIds.RetroCard);

    public ILocator PointRows => Section.GetByTestId(RetroSharedTestIds.RetroPointRow);

    public ILocator ThemeChips => Section.GetByTestId(RetroSharedTestIds.RetroThemeChip);

    public ILocator AuthorChips => Section.GetByTestId(RetroSharedTestIds.RetroAuthorChip);

    public ILocator LogToggle => Section.GetByTestId(RetroSharedTestIds.RetroCardLogToggle);

    public ILocator Log => Section.GetByTestId(RetroSharedTestIds.RetroCardLog);
}
