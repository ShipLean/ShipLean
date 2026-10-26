using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Feature;

// The Feature's status band: the read view, Still true, and the band editor.
public sealed class StatusBandPanel(IPage page)
{
    public ILocator Band => page.GetByTestId(FeatureTestIds.StatusBand);

    public ILocator StatusLine => page.GetByTestId(FeatureTestIds.StatusLine);

    public ILocator Verified => page.GetByTestId(FeatureTestIds.StatusVerified);

    public ILocator StillTrue => page.GetByTestId(FeatureTestIds.StatusStillTrue);

    public ILocator EditStatus => page.GetByTestId(FeatureTestIds.StatusEdit);

    public ILocator EditLine => page.GetByTestId(FeatureTestIds.StatusEditLine);

    public ILocator Done => page.GetByTestId(FeatureTestIds.StatusEditSave);

    // Clicks Still true and returns the POST response for the item's status confirmation.
    public Task<IResponse> ConfirmAsync(Guid itemId) =>
        page.RunAndWaitForApiAsync($"/api/work-items/{itemId}/status/confirm", "POST", () => StillTrue.ClickAsync());
}
