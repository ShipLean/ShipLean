using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros;

// The Retrospective section on a work item's form: one row per retro of that item, with counts. A row opens the item's page in Retrospectives.
public sealed class WorkItemRetroSection(IPage page)
{
    public ILocator Section => page.GetByTestId(RetrosTestIds.WorkItemRetroSection);

    public ILocator Rows => Section.GetByTestId(RetrosTestIds.WorkItemRetroRow);
}
