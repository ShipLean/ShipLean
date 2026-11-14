using System.Text;
using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Side;
using ShipLean.AcceptanceTests.Pages.Feature.Touch;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// A long Markdown attachment on a phone against the real server (SHIP-715, L715a): the Contents pill names the heading
// being read, its sheet lists the outline, and a pick scrolls to the heading and closes the sheet. The touch rules are
// checked on this task's regions only (the header, the pill and its sheet); the body and editor are other tasks'.
[TestFixture]
public class AttachmentPageTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Contents_WhenPillTappedOnPhone_ShouldJumpToHeading()
    {
        var attachment = new AttachmentPageTouch(Page);
        var touch = new TouchShell(Page);
        var markdown = new StringBuilder("# Saved cards\n\n");
        for (var section = 1; section <= 8; section++)
        {
            markdown.Append($"## Section {section}\n\n");
            for (var line = 0; line < 12; line++)
                markdown.Append($"Paragraph {line} of section {section}: the vault keeps tokens, never card numbers.\n\n");
        }

        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship101, "long-notes.md", Encoding.UTF8.GetBytes(markdown.ToString()));
        await attachment.OpenAsync("SHIP", "SHIP-101", await FindAttachmentIdAsync(SeedIds.WorkItems.Ship101, "long-notes.md"));
        await Expect(attachment.ContentsPill).ToContainTextAsync("Saved cards");
        (await TouchRules.ViolationsAsync(Page, attachment.Header)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, attachment.ContentsPill)).ShouldBeEmpty();
        await Expect(touch.TopBarBack).ToBeVisibleAsync();

        // Reading on: the pill follows the heading that has passed the top.
        await attachment.ScrollHeadingToTopAsync("Section 3");
        await Expect(attachment.ContentsPill).ToContainTextAsync("Section 3");

        await attachment.ContentsPill.TapAsync();

        var sheet = touch.Sheet("On this page");
        await Expect(sheet).ToBeVisibleAsync();
        await Expect(attachment.ContentsEntries).ToHaveCountAsync(9);
        (await TouchRules.ViolationsAsync(Page, sheet)).ShouldBeEmpty();
        await attachment.ContentsEntries.Filter(new LocatorFilterOptions { HasText = "Section 7" }).TapAsync();

        await Expect(touch.Sheets).ToHaveCountAsync(0);
        // Under the top bar's own height, not behind it.
        (await attachment.HeadingTopAsync("Section 7")).ShouldBeInRange(40, 120);
        await Expect(attachment.ContentsPill).ToContainTextAsync("Section 7");
    }

    private async Task<Guid> FindAttachmentIdAsync(Guid itemId, string name)
    {
        var response = await Page.Context.APIRequest.GetAsync($"{BaseUrl}/api/work-items/{itemId}/attachments");
        response.Ok.ShouldBeTrue();
        using var json = JsonDocument.Parse(await response.BodyAsync());
        return json.RootElement.EnumerateArray()
            .First(a => a.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();
    }
}
