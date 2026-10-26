using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Feature;

// The Feature form's Links section: work item rows and URL cards.
public sealed class LinksSectionPanel(IPage page)
{
    public ILocator Section => page.GetByTestId(FeatureTestIds.Links);

    public ILocator Count => Section.GetByTestId(FeatureTestIds.LinksCount);

    public ILocator Cards => Section.GetByTestId(FeatureTestIds.LinkCard);

    public ILocator CardTitled(string title) =>
        Cards.Filter(new LocatorFilterOptions { Has = page.GetByTestId(FeatureTestIds.LinkCardTitle).Filter(new LocatorFilterOptions { HasText = title }) });
}
