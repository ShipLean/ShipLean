using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// The Retrospective section on a work item's form: one row per retro of that item, with counts. A row opens the item's page in Retrospectives.
public sealed class WorkItemRetroSection(IPage page)
{
    public ILocator Section => page.GetByTestId(RetrosTestIds.WorkItemRetroSection);

    public ILocator Rows => Section.GetByTestId(RetrosTestIds.WorkItemRetroRow);

    // TODO: J2 moves FeatureRetroJourneyTests and AgentRetroJourneyTests to the item page and deletes these card members, which the section no longer renders.
    public ILocator Cards => Section.GetByTestId(RetroSharedTestIds.RetroCard);

    public ILocator AuthorChips => Section.GetByTestId(RetroSharedTestIds.RetroAuthorChip);

    public ILocator LogToggle => Section.GetByTestId(RetroSharedTestIds.RetroCardLogToggle);

    public ILocator Log => Section.GetByTestId(RetroSharedTestIds.RetroCardLog);
}
