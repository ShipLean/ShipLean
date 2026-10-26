using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Feature;

// The Feature's attachments rail and the drop overlay.
public sealed class AttachmentsRailPanel(IPage page)
{
    public ILocator Rail => page.GetByTestId(FeatureTestIds.AttachmentsRail);

    public ILocator Count => page.GetByTestId(FeatureTestIds.RailCount);

    public ILocator DropInput => page.GetByTestId(FeatureTestIds.DropInput);

    public ILocator DropOutcomes => page.GetByTestId(FeatureTestIds.DropOutcome);

    public ILocator DropUpload => page.GetByTestId(FeatureTestIds.DropUpload);

    public ILocator DropOverlay => page.GetByTestId(FeatureTestIds.DropOverlay);

    public ILocator Items => Rail.GetByTestId(FeatureTestIds.RailItem);

    public ILocator RemoveOf(string name) =>
        Items.Filter(new LocatorFilterOptions { HasText = name }).GetByTestId(FeatureTestIds.RailItemRemove);

    public ILocator Markdown => Rail.GetByTestId(FeatureTestIds.RailGroupMarkdown);

    public ILocator Images => Rail.GetByTestId(FeatureTestIds.RailGroupImage);

    public ILocator Files => Rail.GetByTestId(FeatureTestIds.RailGroupFile);

    public Task DropAsync(params (string Name, string ContentType)[] files) =>
        DropInput.SetInputFilesAsync(files.Select(file => new FilePayload
        {
            Name = file.Name,
            MimeType = file.ContentType,
            Buffer = System.Text.Encoding.UTF8.GetBytes($"content of {file.Name}"),
        }).ToArray());
}
