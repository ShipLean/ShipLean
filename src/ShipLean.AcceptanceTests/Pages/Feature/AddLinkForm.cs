using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Feature;

// The Feature form's + Add link: the button, then the form with its URL and work item tabs.
public sealed class AddLinkForm(IPage page)
{
    public ILocator Root => page.GetByTestId(FeatureTestIds.AddLink);

    public ILocator Open => Root.GetByTestId(FeatureTestIds.AddLinkOpen);

    public ILocator Form => Root.GetByTestId(FeatureTestIds.AddLinkForm);

    public ILocator Url => Root.GetByTestId(FeatureTestIds.AddLinkUrl);

    public ILocator Title => Root.GetByTestId(FeatureTestIds.AddLinkTitle);

    public ILocator Note => Root.GetByTestId(FeatureTestIds.AddLinkNote);

    public ILocator Message => Root.GetByTestId(FeatureTestIds.AddLinkMessage);

    public ILocator Submit => Root.GetByTestId(FeatureTestIds.AddLinkSubmit);

    public ILocator Error => Root.GetByTestId(FeatureTestIds.AddLinkError);
}
