using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Feature;

// The Feature form's Goal, field strip and Stories and bugs regions.
public sealed class FeatureStripPanel(IPage page)
{
    public ILocator GoalEdit => page.GetByTestId(FeatureTestIds.GoalEdit);

    public ILocator GoalInput => page.GetByTestId(FeatureTestIds.GoalInput);

    public ILocator GoalView => page.GetByTestId(FeatureTestIds.GoalView);

    public ILocator Slug => page.GetByTestId(FeatureTestIds.StripSlug);

    public ILocator StripChildrenCount => page.GetByTestId(FeatureTestIds.StripChildrenCount);

    public ILocator ChildrenCount => page.GetByTestId(FeatureTestIds.ChildrenCount);

    public ILocator EpicOpen => page.GetByTestId(FeatureTestIds.EpicOpen);

    public ILocator ChildTitles => page.GetByTestId(FeatureTestIds.ChildTitle);
}
