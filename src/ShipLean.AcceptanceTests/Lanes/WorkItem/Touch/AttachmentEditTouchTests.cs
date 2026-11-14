using System.Text;
using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Side;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// Editing a Markdown attachment on a phone against the real server (SHIP-715, L715b): the toolbar takes the top bar's
// pinned spot, the Save bar carries Discard and Save. The touch rules are checked on this task's regions only (the
// editor, its toolbar and the Save bar).
[TestFixture]
public class AttachmentEditTouchTests : SeededAcceptanceTest
{
    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Edit_WhenSavedFromSaveBarOnPhone_ShouldReplaceFile()
    {
        var attachment = new AttachmentPage(Page);
        var touch = new TouchShell(Page);
        await OpenEditorAsync(attachment, "notes.md", "# First draft\n");
        await Expect(touch.SaveBar).ToHaveCountAsync(0);

        await attachment.EditorInput.FillAsync("# Second draft\n\nEdited on a phone.\n");

        await Expect(touch.SaveBar).ToBeVisibleAsync();
        await Expect(touch.SaveBarMessage).ToHaveTextAsync("Saving replaces the file");
        await AssertRegionsAsync(touch);
        await touch.SaveBarSave.TapAsync();

        await Expect(attachment.Body).ToContainTextAsync("Edited on a phone.");
        await Expect(touch.SaveBar).ToHaveCountAsync(0);
        await Expect(touch.TopBarPinned).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Edit_WhenDiscardedOnPhone_ShouldDropDraft()
    {
        var attachment = new AttachmentPage(Page);
        var touch = new TouchShell(Page);
        await OpenEditorAsync(attachment, "notes.md", "# First draft\n");

        await attachment.EditorInput.FillAsync("# Throwaway\n");
        await touch.SaveBarDiscard.TapAsync();

        await Expect(attachment.Body).ToContainTextAsync("First draft");
        await Expect(attachment.Body).Not.ToContainTextAsync("Throwaway");
        await Expect(touch.SaveBar).ToHaveCountAsync(0);
        await Expect(touch.TopBarPinned).ToHaveCountAsync(0);
        await Expect(attachment.EditorInput).ToHaveCountAsync(0);
    }

    private async Task AssertRegionsAsync(TouchShell touch)
    {
        (await TouchRules.ViolationsAsync(Page, Page.GetByTestId(AttachmentTestIds.Editor))).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, touch.TopBarPinned)).ShouldBeEmpty();
        (await TouchRules.ViolationsAsync(Page, touch.SaveBar)).ShouldBeEmpty();
    }

    private async Task OpenEditorAsync(AttachmentPage attachment, string name, string markdown)
    {
        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship101, name, Encoding.UTF8.GetBytes(markdown));
        var response = await Page.Context.APIRequest.GetAsync($"{BaseUrl}/api/work-items/{SeedIds.WorkItems.Ship101}/attachments");
        response.Ok.ShouldBeTrue();
        using var json = JsonDocument.Parse(await response.BodyAsync());
        var id = json.RootElement.EnumerateArray()
            .First(a => a.GetProperty("name").GetString() == name)
            .GetProperty("id").GetGuid();
        await attachment.OpenAsync("SHIP", "SHIP-101", id);
        await Expect(attachment.Edit).ToBeVisibleAsync();
        await attachment.Edit.TapAsync();
        await Expect(attachment.EditorInput).ToBeVisibleAsync();
        await Expect(new TouchShell(Page).TopBarPinned).ToBeVisibleAsync();
    }
}
