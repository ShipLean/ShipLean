using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.WorkItem.Touch;

// The work item's fields block at 900px and below (SHIP-714 D5): two-column tiles whose State, Assignee and Parent open
// bottom sheets (the sheet itself is TouchShell's). Every action taps.
public sealed class FieldSheets(IPage page)
{
    public ILocator Fields => page.GetByTestId(WorkItemTestIds.Fields);

    public ILocator StateButton => page.GetByTestId(WorkItemTestIds.StateButton);

    public ILocator ParentButton => page.GetByTestId(WorkItemTestIds.ParentButton);

    public ILocator ParentSearch => page.GetByTestId(WorkItemTestIds.ParentSearch);

    public ILocator StateOptions => page.GetByTestId(ComponentTestIds.StateMenuOption);

    public ILocator StateOption(string name) => StateOptions.Filter(new LocatorFilterOptions { HasText = name });

    public ILocator ParentOption(string text) =>
        page.GetByTestId(WorkItemTestIds.ParentOption).Filter(new LocatorFilterOptions { HasText = text });

    // The unsaved-change dots in the fields block (one per changed field).
    public ILocator ChangedDots => Fields.GetByTestId(WorkItemTestIds.ChangedDot);
}
