using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Side;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Feature;

[TestFixture]
public class AttachmentPageTests : SeededAcceptanceTest
{
    [Test]
    public async Task AttachmentPage_WhenOpenedFromUrl_ShouldShowRenderedMarkdown()
    {
        var attachment = new AttachmentPage(Page);
        var markdown = "# Saved card design\n\nTokens stay in the vault.\n\n## Open questions\n\nHide expired cards?\n";
        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship101, "design-v0.2.md", Encoding.UTF8.GetBytes(markdown));
        var id = await FindAttachmentIdAsync(SeedIds.WorkItems.Ship101, "design-v0.2.md");

        await attachment.OpenAsync("SHIP", "SHIP-101", id);

        await Expect(attachment.Name).ToHaveTextAsync("design-v0.2.md");
        await Expect(attachment.WorkItem).ToContainTextAsync("SHIP-101");
        await Expect(attachment.Meta).ToContainTextAsync("uploaded by Tom");
        await Expect(attachment.Body).ToContainTextAsync("Saved card design");
        await Expect(attachment.Body).ToContainTextAsync("Tokens stay in the vault.");
    }

    [Test]
    public async Task AttachmentPage_WhenEditedAndSaved_ShouldShowTheNewCopy()
    {
        var attachment = new AttachmentPage(Page);
        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship101, "notes.md", Encoding.UTF8.GetBytes("# First draft\n"));
        var id = await FindAttachmentIdAsync(SeedIds.WorkItems.Ship101, "notes.md");
        await attachment.OpenAsync("SHIP", "SHIP-101", id);

        await attachment.Edit.ClickAsync();
        await attachment.EditorInput.FillAsync("# Second draft\n\nEdited in place.\n");
        await attachment.Save.ClickAsync();

        // Saving replaces the file under the same name, so the page moves to the new copy's id.
        await Expect(attachment.Body).ToContainTextAsync("Edited in place.");
        await Expect(attachment.Meta).ToContainTextAsync("replaced once");
        var newId = await FindAttachmentIdAsync(SeedIds.WorkItems.Ship101, "notes.md");
        newId.ShouldNotBe(id);
        await Expect(Page).ToHaveURLAsync(new Regex($"/attachments/{newId}$"));
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
