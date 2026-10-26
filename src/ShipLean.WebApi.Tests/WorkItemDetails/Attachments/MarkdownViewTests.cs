using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Markdown;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// GET api/attachments/{id}/markdown: the outline, images and siblings, and the refusals that are not about size.
[TestFixture]
public class MarkdownViewTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<IBlobStore>(new LocalDiskBlobStore(BlobRoot));

    [OneTimeTearDown]
    public void DeleteBlobs() => Directory.Delete(BlobRoot, recursive: true);

    private Task<AttachmentDto> UploadAsync(string name, string contentType, string text)
        => AttachmentUploads.UploadAsync(Client, ItemUrl, name, contentType, Encoding.UTF8.GetBytes(text));

    private async Task<AttachmentMarkdownDto> ViewAsync(string name, string text)
    {
        var added = await UploadAsync(name, "text/markdown", text);
        return await AttachmentWire.GetAsync<AttachmentMarkdownDto>(Api, $"api/attachments/{added.Id}/markdown");
    }

    [Test]
    public async Task MarkdownView_WhenHeadings_ShouldReturnOutlineWithAnchors()
    {
        var view = await ViewAsync("headings.md", "# Plan\n\n## Step `one`\n\n### Deep\n");

        view.Outline.Select(o => (o.Level, o.Text, o.Anchor)).ShouldBe([(1, "Plan", "plan"), (2, "Step one", "step-one"), (3, "Deep", "deep")]);
        foreach (var entry in view.Outline)
        {
            view.Html.ShouldContain($"id=\"{entry.Anchor}\"");
        }
    }

    [Test]
    public async Task MarkdownView_ShouldListSiblingMarkdownOnly()
    {
        var other = await UploadAsync("sibling-a.md", "text/markdown", "# a");
        var archived = await UploadAsync("sibling-gone.md", "text/markdown", "# gone");
        (await Api.PostJsonAsync($"api/attachments/{archived.Id}/archive")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await UploadAsync("sibling.png", "image/png", "x");

        var view = await ViewAsync("sibling-main.md", "# main");

        view.Siblings.Select(s => s.Id).ShouldContain(other.Id);
        view.Siblings.Select(s => s.Id).ShouldNotContain(archived.Id);
        view.Siblings.Select(s => s.Id).ShouldNotContain(view.Attachment.Id);
        view.Siblings.ShouldAllBe(s => s.Kind == AttachmentKind.Markdown);
    }

    [Test]
    public async Task MarkdownView_WhenExternalImage_ShouldListItExternal()
    {
        var view = await ViewAsync("external.md", "![remote](https://example.com/pic.png)");

        view.Images.ShouldBe([new MarkdownImageDto("https://example.com/pic.png", MarkdownImageStatus.External, null)]);
    }

    [Test]
    public async Task MarkdownView_WhenNotMarkdown_ShouldReturnNotMarkdown()
    {
        var added = await UploadAsync("notes.txt", "text/plain", "plain");

        using var response = await Api.GetAsync($"api/attachments/{added.Id}/markdown");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.NotMarkdown);
    }

    [Test]
    public async Task MarkdownView_WhenNonMember_ShouldReturnNotFound()
    {
        var added = await UploadAsync("members-only.md", "text/markdown", "# private");

        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/attachments/{added.Id}/markdown");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task MarkdownView_WhenArchived_ShouldReturnNotFound()
    {
        var added = await UploadAsync("archived.md", "text/markdown", "# old");
        (await Api.PostJsonAsync($"api/attachments/{added.Id}/archive")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await Api.GetAsync($"api/attachments/{added.Id}/markdown");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
