using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Retros.Touch;

// Retrospectives › Features at 900px and below (SHIP-728): the phone list and its one-row toolbar, and the tablet table's
// box. Use with RetroFeaturesPage for the panel, rows, filter options and the search field.
public sealed class RetroFeaturesTouch(IPage page)
{
    public ILocator PhoneList => page.GetByTestId(RetrosTestIds.RetroFeaturesPhoneList);

    public ILocator ListHead => page.GetByTestId(RetrosTestIds.RetroFeaturesListHead);

    public ILocator Count => page.GetByTestId(RetrosTestIds.RetroFeaturesCount);

    public ILocator LegendItems => page.GetByTestId(RetrosTestIds.RetroFeaturesLegendItem);

    public ILocator SearchOpen => page.GetByTestId(RetrosTestIds.RetroFeaturesSearchOpen);

    public ILocator SearchCancel => page.GetByTestId(RetrosTestIds.RetroFeaturesSearchCancel);

    public ILocator Empty => page.GetByTestId(RetrosTestIds.RetroFeaturesEmpty);

    public ILocator ClearSearch => page.GetByTestId(RetrosTestIds.RetroFeaturesClearSearch);

    public ILocator TableBox => page.GetByTestId(RetrosTestIds.RetroFeaturesTableBox);

    public static ILocator MetaOf(ILocator row) => row.GetByTestId(RetrosTestIds.RetroFeaturesMeta);

    public static ILocator ChipOf(ILocator row) => row.GetByTestId(RetrosTestIds.RetroFeaturesNoFeatureChip);

    public static ILocator FeatureCellOf(ILocator row) => row.GetByTestId(RetrosTestIds.RetroFeaturesFeatureCell);
}
