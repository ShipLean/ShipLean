using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Import;

// Step 4 of the Jira import stepper, and the run step a skipped mapping lands on.
public sealed class ImportMappingPage(IPage page)
{
    public ILocator Step => page.GetByTestId(ImportTestIds.MappingStep);

    public ILocator NewChips => page.GetByTestId(ImportTestIds.MappingNewChip);

    public ILocator StatusRows => page.GetByTestId(ImportTestIds.MappingStatusRow);

    public ILocator NewStatusRows => StatusRows.Filter(new LocatorFilterOptions { Has = NewChips });

    public ILocator Run => page.GetByTestId(ImportTestIds.RunStep);

    public ILocator MappingStepSummary => page.GetByTestId(ImportTestIds.Step4);

    public ILocator Next => page.GetByTestId(ImportTestIds.Next);
}
