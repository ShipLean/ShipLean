using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Feature;

// The Feature form's regions, each by its root: the main column top to bottom, then the rail.
public sealed class FeatureLayout(IPage page)
{
    // The whole form sits inside the drop zone.
    public ILocator FormInDropZone => page.GetByTestId(FeatureTestIds.DropZone).GetByTestId(FeatureTestIds.Form);

    public ILocator AddLink => page.GetByTestId(FeatureTestIds.AddLink);

    public IReadOnlyList<ILocator> MainColumn =>
    [
        page.GetByTestId(FeatureTestIds.Strip),
        page.GetByTestId(FeatureTestIds.StatusBand),
        page.GetByTestId(FeatureTestIds.Goal),
        page.GetByTestId(WorkItemTestIds.Description),
        page.GetByTestId(FeatureTestIds.Links),
        page.GetByTestId(FeatureTestIds.StoriesAndBugs),
        page.GetByTestId(FeatureTestIds.Log),
    ];

    public IReadOnlyList<ILocator> Rail =>
    [
        page.GetByTestId(FeatureTestIds.AttachmentsRail),
        page.GetByTestId(WorkItemTestIds.History),
    ];
}
