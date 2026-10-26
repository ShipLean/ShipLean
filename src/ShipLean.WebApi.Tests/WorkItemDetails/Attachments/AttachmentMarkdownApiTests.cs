using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Markdown;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// GET api/attachments/{id}/markdown: the real part of the endpoint (the file, its item, source and HTML) and its refusals.
[TestFixture]
public class AttachmentMarkdownApiTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<IBlobStore>(new LocalDiskBlobStore(BlobRoot));

    [OneTimeTearDown]
    public void DeleteBlobs() => Directory.Delete(BlobRoot, recursive: true);

    private Task<AttachmentDto> UploadMarkdownAsync(string name, string text)
        => AttachmentUploads.UploadAsync(Client, ItemUrl, name, "text/markdown", Encoding.UTF8.GetBytes(text));

    [Test]
    public async Task MarkdownView_WhenMarkdownUploaded_ShouldReturnItsSourceAndHtml()
    {
        var added = await UploadMarkdownAsync("view-me.md", "# Hello\n\nsome **text**\n");

        var view = await AttachmentWire.GetAsync<AttachmentMarkdownDto>(Api, $"api/attachments/{added.Id}/markdown");

        view.Attachment.Id.ShouldBe(added.Id);
        view.Markdown.ShouldBe("# Hello\n\nsome **text**\n");
        view.Html.ShouldContain("<strong>text</strong>");
        view.WorkItem.Id.ShouldBe(SeedIds.WorkItems.Ship90);
        view.WorkItem.Key.ShouldNotBeNullOrWhiteSpace();
        view.WorkItem.Title.ShouldNotBeNullOrWhiteSpace();
        view.WorkItem.Type.Id.ShouldBe(WorkItemTypeId.Epic);
    }

    [Test]
    public async Task MarkdownView_WhenNotMarkdown_ShouldBeNotMarkdown()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "plain.txt", "text/plain", [65]);

        using var response = await Api.GetAsync($"api/attachments/{added.Id}/markdown");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.NotMarkdown);
    }

    [Test]
    public async Task MarkdownView_WhenOverTheFileLimit_ShouldBeMarkdownTooLarge()
    {
        var added = await UploadMarkdownAsync("huge.md", new string('a', 256 * 1024 + 1));

        using var response = await Api.GetAsync($"api/attachments/{added.Id}/markdown");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.MarkdownTooLarge);
    }

    [Test]
    public async Task MarkdownView_WhenFileIsOverTheTypedLimit_ShouldRender()
    {
        var added = await UploadMarkdownAsync("long.md", "# Long\n\n" + string.Concat(Enumerable.Repeat("some **text** here.\n\n", 10_000)));

        var view = await AttachmentWire.GetAsync<AttachmentMarkdownDto>(Api, $"api/attachments/{added.Id}/markdown");

        view.Markdown.Length.ShouldBeGreaterThan(MarkdownRenderer.MaxLength);
        view.Html.ShouldContain("<h1");
        view.Html.ShouldContain("<strong>text</strong>");
    }

    [Test]
    public async Task MarkdownView_WhenNotMember_ShouldBeNotFound()
    {
        var added = await UploadMarkdownAsync("private.md", "# secret");

        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/attachments/{added.Id}/markdown");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task MarkdownView_WhenArchived_ShouldBeNotFound()
    {
        var added = await UploadMarkdownAsync("gone.md", "# gone");
        (await Api.PostJsonAsync($"api/attachments/{added.Id}/archive")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await Api.GetAsync($"api/attachments/{added.Id}/markdown");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
