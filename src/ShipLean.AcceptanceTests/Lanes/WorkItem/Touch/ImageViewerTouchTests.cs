using System.Text;
using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Lanes.WorkItem.Side;
using ShipLean.AcceptanceTests.Pages.Feature;
using ShipLean.AcceptanceTests.Pages.Feature.Touch;
using ShipLean.AcceptanceTests.Pages.Touch;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Touch;

// The full-screen image viewer on a phone against the real server (SHIP-715, L715c): a tapped rail image or Markdown
// image opens it on that image among all of the item's images, and a swipe goes to the next. The touch rules are checked
// on the viewer only; the rest of each screen is other tasks'.
[TestFixture]
public class ImageViewerTouchTests : SeededAcceptanceTest
{
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    protected override AcceptanceViewport Viewport => AcceptanceViewport.Phone;

    [Test]
    public async Task Viewer_WhenRailImageTappedOnPhone_ShouldOpenAtThatImageAndSwipeToNext()
    {
        var rail = new AttachmentsRailPanel(Page);
        var touch = new TouchShell(Page);
        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship101, "rail-one.png", Png);
        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship101, "rail-two.png", Png);
        var images = await ImageNamesAsync(SeedIds.WorkItems.Ship101);
        await new WorkItemScreen(Page).OpenAsync("SHIP", "SHIP-101");

        await rail.Items.Filter(new LocatorFilterOptions { HasText = "rail-one.png" }).First.TapAsync();

        var counter = Page.GetByTestId(AttachmentTestIds.ViewerCounter);
        var first = images.IndexOf("rail-one.png") + 1;
        await Expect(touch.ImageViewer).ToBeVisibleAsync();
        await Expect(counter).ToHaveTextAsync($"{first} of {images.Count} · SHIP-101");
        (await TouchRules.ViolationsAsync(Page, touch.ImageViewer)).ShouldBeEmpty();

        await SwipeLeftAsync();

        await Expect(counter).ToHaveTextAsync($"{first + 1} of {images.Count} · SHIP-101");
        await touch.ImageViewerClose.TapAsync();
        await Expect(touch.ImageViewer).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Viewer_WhenMarkdownImageTappedOnPhone_ShouldOpenAtItWithAllTheItemsImages()
    {
        var attachment = new AttachmentPageTouch(Page);
        var touch = new TouchShell(Page);
        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship101, "md-one.png", Png);
        await WorkItemAttachments.UploadAsTomAsync(BaseUrl, SeedIds.WorkItems.Ship101, "md-two.png", Png);
        await WorkItemAttachments.UploadAsTomAsync(
            BaseUrl, SeedIds.WorkItems.Ship101, "gallery.md", Encoding.UTF8.GetBytes("# Gallery\n\n![two](md-two.png)\n"));
        var images = await ImageNamesAsync(SeedIds.WorkItems.Ship101);
        var galleryId = await FindAttachmentIdAsync(SeedIds.WorkItems.Ship101, "gallery.md");
        // A tap before the item's images have loaded opens the viewer on the page's own images only, so wait for them.
        await Page.RunAndWaitForResponseAsync(
            () => attachment.OpenAsync("SHIP", "SHIP-101", galleryId),
            response => response.Url.EndsWith($"/api/work-items/{SeedIds.WorkItems.Ship101}/attachments", StringComparison.Ordinal));
        var body = Page.GetByTestId(AttachmentTestIds.Body);
        await Expect(body).ToContainTextAsync("Gallery");

        // The rendered image has no test id of its own, so it is reached by its alt text through the body, and tapped.
        var center = await body.EvaluateAsync<double[]>(
            "(root) => { const r = root.querySelector('figure[data-md-image=attached] img[alt=two]').getBoundingClientRect(); return [r.x + r.width / 2, r.y + r.height / 2]; }");
        await Page.Touchscreen.TapAsync((float)center[0], (float)center[1]);

        var counter = Page.GetByTestId(AttachmentTestIds.ViewerCounter);
        await Expect(touch.ImageViewer).ToBeVisibleAsync();
        await Expect(counter).ToHaveTextAsync($"{images.IndexOf("md-two.png") + 1} of {images.Count} · SHIP-101");
        (await TouchRules.ViolationsAsync(Page, touch.ImageViewer)).ShouldBeEmpty();
    }

    // A one-finger drag from the right of the image to its left, which is the swipe to the next image.
    private async Task SwipeLeftAsync()
    {
        var box = await Page.GetByTestId(AttachmentTestIds.ViewerStage).BoundingBoxAsync();
        box.ShouldNotBeNull();
        var y = box.Y + (box.Height / 2);
        await Page.Mouse.MoveAsync(box.X + box.Width - 40, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + (box.Width / 2), y, new MouseMoveOptions { Steps = 4 });
        await Page.Mouse.MoveAsync(box.X + 40, y, new MouseMoveOptions { Steps = 4 });
        await Page.Mouse.UpAsync();
    }

    // The item's image files in the order the server lists them, which is the order the viewer swipes through.
    private async Task<List<string>> ImageNamesAsync(Guid itemId)
    {
        var response = await Page.Context.APIRequest.GetAsync($"{BaseUrl}/api/work-items/{itemId}/attachments");
        response.Ok.ShouldBeTrue();
        using var json = JsonDocument.Parse(await response.BodyAsync());
        return json.RootElement.EnumerateArray()
            .Where(a => a.GetProperty("kind").GetString() == "image")
            .Select(a => a.GetProperty("name").GetString()!)
            .ToList();
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
