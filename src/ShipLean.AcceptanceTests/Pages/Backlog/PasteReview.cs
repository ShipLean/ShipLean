using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Backlog;

// The Add many dialog: the pasted text, the review grid, the "Put them" choice and Add.
public sealed class PasteReview(IPage page)
{
    public ILocator Dialog => page.GetByTestId(BacklogTestIds.PasteDialog);

    public ILocator Source => page.GetByTestId(BacklogTestIds.PasteSource);

    public ILocator Rows => page.GetByTestId(BacklogTestIds.PasteRow);

    public ILocator Duplicates => page.GetByTestId(BacklogTestIds.PasteRowDuplicate);

    public ILocator Place => page.GetByTestId(BacklogTestIds.PastePlace);

    public ILocator Create => page.GetByTestId(BacklogTestIds.PasteCreate);

    public ILocator KeepOne => page.GetByTestId(BacklogTestIds.PasteKeepOne);

    // Types the text into the box, as the Add many button's empty box takes it.
    public Task FillAsync(string text) => Source.FillAsync(text);

    // Presses Add and returns the create response (K2).
    public Task<IResponse> CreateAsync() =>
        page.RunAndWaitForApiAsync($"/api/projects/{SeedIds.Projects.Ship}/backlog/items", "POST", () => Create.ClickAsync());
}
