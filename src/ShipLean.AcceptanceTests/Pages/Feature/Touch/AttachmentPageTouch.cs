using ShipLean.AcceptanceTests.Harness;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Pages.Feature.Touch;

// A Markdown attachment's phone header and Contents pill (SHIP-715 D1, D2, D7, D8). The sheets themselves are
// TouchShell's. Every action taps.
public sealed class AttachmentPageTouch(IPage page)
{
    public ILocator Header => page.GetByTestId(AttachmentTestIds.Header);

    public ILocator NameButton => page.GetByTestId(AttachmentTestIds.NameButton);

    public ILocator Facts => page.GetByTestId(AttachmentTestIds.Facts);

    public ILocator ContentsPill => page.GetByTestId(AttachmentTestIds.ContentsPill);

    public ILocator ContentsEntries => page.GetByTestId(AttachmentTestIds.ContentsEntry);

    // The rendered body's headings have no test id of their own, so they are reached by their text through the body.
    public Task ScrollHeadingToTopAsync(string text) => page.GetByTestId(AttachmentTestIds.Body).EvaluateAsync(
        "(body, text) => [...body.querySelectorAll('h1,h2,h3')].find(h => h.textContent === text).scrollIntoView({ block: 'start' })",
        text);

    // The heading's distance from the top of the viewport, in CSS pixels.
    public Task<double> HeadingTopAsync(string text) => page.GetByTestId(AttachmentTestIds.Body).EvaluateAsync<double>(
        "(body, text) => [...body.querySelectorAll('h1,h2,h3')].find(h => h.textContent === text).getBoundingClientRect().top",
        text);

    public async Task OpenAsync(string projectKey, string itemKey, Guid attachmentId)
    {
        await page.GotoAsync($"{Links.WorkItem(projectKey, itemKey)}/attachments/{attachmentId}");
        await NameButton.WaitForAsync();
    }
}
