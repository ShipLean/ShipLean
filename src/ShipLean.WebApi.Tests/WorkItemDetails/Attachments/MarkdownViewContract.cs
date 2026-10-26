using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Markdown;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// What the Markdown view must also answer once it is real: the outline, the images and the item's other Markdown files.
// Red until A4 (the stub returns all three empty; the attachment, item, Markdown and HTML are already real).
[TestFixture]
public class MarkdownViewContract : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<IBlobStore>(new LocalDiskBlobStore(BlobRoot));

    [OneTimeTearDown]
    public void DeleteBlobs() => Directory.Delete(BlobRoot, recursive: true);

    private async Task<AttachmentMarkdownDto> ViewAsync(string name, string text)
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, name, "text/markdown", Encoding.UTF8.GetBytes(text));
        return await AttachmentWire.GetAsync<AttachmentMarkdownDto>(Api, $"api/attachments/{added.Id}/markdown");
    }

    [Test]
    public async Task MarkdownView_WhenHeadings_ShouldReturnTheOutlineWithTheRenderedIds()
    {
        var view = await ViewAsync("outline.md", "# One\n\n## Two\n");

        view.Outline.ShouldBe([new MarkdownOutlineEntryDto(1, "One", "one"), new MarkdownOutlineEntryDto(2, "Two", "two")]);
        view.Html.ShouldContain("id=\"two\"");
    }

    [Test]
    public async Task MarkdownView_WhenImages_ShouldClassifyThemAgainstTheItem()
    {
        var picture = await AttachmentUploads.UploadAsync(Client, ItemUrl, "diagram.png", "image/png", [1, 2]);

        var view = await ViewAsync("images.md", "![a](diagram.png) ![b](https://example.com/b.png) ![c](nope.png)");

        view.Images.ShouldBe(
        [
            new MarkdownImageDto("diagram.png", MarkdownImageStatus.Attached, picture.Id),
            new MarkdownImageDto("https://example.com/b.png", MarkdownImageStatus.External, null),
            new MarkdownImageDto("nope.png", MarkdownImageStatus.Missing, null),
        ]);
    }

    [Test]
    public async Task MarkdownView_WhenTheItemHasOtherMarkdownFiles_ShouldListThemAsSiblings()
    {
        var other = await AttachmentUploads.UploadAsync(Client, ItemUrl, "sibling.md", "text/markdown", "# other"u8.ToArray());
        await AttachmentUploads.UploadAsync(Client, ItemUrl, "not-markdown.png", "image/png", [1]);

        var view = await ViewAsync("main.md", "# main");

        view.Siblings.Select(s => s.Id).ShouldContain(other.Id);
        view.Siblings.ShouldAllBe(s => s.Kind == AttachmentKind.Markdown && s.Id != view.Attachment.Id);
    }
}
