using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// The New Feature dialog opened from the Features backlog.
public sealed class NewFeatureDialog(IPage page)
{
    public ILocator NewButton => page.GetByTestId(BacklogTestIds.NewItem);

    public ILocator Title => page.GetByTestId(BacklogTestIds.NewFeatureTitle);

    public ILocator Slug => page.GetByTestId(BacklogTestIds.NewFeatureSlug);

    public ILocator SlugNote => page.GetByTestId(BacklogTestIds.NewFeatureSlugNote);

    public ILocator Goal => page.GetByTestId(BacklogTestIds.NewFeatureGoal);

    public ILocator Status => page.GetByTestId(BacklogTestIds.NewFeatureStatus);

    public ILocator Create => page.GetByTestId(BacklogTestIds.NewFeatureCreate);

    public ILocator StartAs(string stateName) => page.GetByTestId(BacklogTestIds.NewFeatureStartOption).Filter(new() { HasText = stateName });

    public async Task OpenAsync()
    {
        await NewButton.ClickAsync();
        await Title.WaitForAsync();
    }
}
