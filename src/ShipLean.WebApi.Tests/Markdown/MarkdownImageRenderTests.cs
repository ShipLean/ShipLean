using System.Net;
using System.Net.Http.Json;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Markdown;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Markdown;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

namespace ShipLean.WebApi.Tests.Markdown;

// POST api/markdown/render with a work item: the image list, the access check and the heading ids.
[TestFixture]
public class MarkdownImageRenderTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<IBlobStore>(new LocalDiskBlobStore(BlobRoot));

    [OneTimeTearDown]
    public void DeleteBlobs() => Directory.Delete(BlobRoot, recursive: true);

    private static Task<HttpResponseMessage> RenderAsync(ApiClient client, string markdown, Guid? workItemId)
        => client.PostJsonAsync("api/markdown/render", new MarkdownRenderRequest(markdown, workItemId));

    [Test]
    public async Task Render_WhenWorkItemGiven_ShouldListImages()
    {
        var picker = await AttachmentUploads.UploadAsync(Client, ItemUrl, "picker.png", "image/png", [1, 2, 3]);

        using var response = await RenderAsync(
            Api, "![a](picker.png) ![b](https://example.com/x.png) ![c](nothing.png)", SeedIds.WorkItems.Ship90);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rendered = (await response.Content.ReadFromJsonAsync<RenderedMarkdownDto>())!;
        rendered.Images.ShouldBe(
        [
            new MarkdownImageDto("picker.png", MarkdownImageStatus.Attached, picker.Id),
            new MarkdownImageDto("https://example.com/x.png", MarkdownImageStatus.External, null),
            new MarkdownImageDto("nothing.png", MarkdownImageStatus.Missing, null),
        ]);
    }

    [Test]
    public async Task Render_WhenNoWorkItem_ShouldNotMatchAttachments()
    {
        await AttachmentUploads.UploadAsync(Client, ItemUrl, "unmatched.png", "image/png", [1]);

        using var response = await RenderAsync(Api, "![a](unmatched.png)", null);

        var rendered = (await response.Content.ReadFromJsonAsync<RenderedMarkdownDto>())!;
        rendered.Images.Single().Status.ShouldBe(MarkdownImageStatus.Missing);
    }

    [Test]
    public async Task Render_WhenWorkItemNotMember_ShouldReturnNotFound()
    {
        using var response = await RenderAsync(Api.ActingAs(TestUser.Kai), "![a](picker.png)", SeedIds.WorkItems.Ship90);

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Render_WhenWorkItemUnknown_ShouldReturnNotFound()
    {
        using var response = await RenderAsync(Api, "text", Guid.NewGuid());

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Render_WhenHeadings_ShouldEmitIdsMatchingOutline()
    {
        const string markdown = "# Vault notes\n\n## Tokens and `keys`\n\ntext\n\n## Tokens and `keys`\n\n### **Bold** end\n";

        using var response = await RenderAsync(Api, markdown, null);

        var rendered = (await response.Content.ReadFromJsonAsync<RenderedMarkdownDto>())!;
        var headings = (await new HtmlParser().ParseDocumentAsync(rendered.Html)).QuerySelectorAll("h1, h2, h3, h4, h5, h6").ToList();
        var outline = MarkdownHeadings.Outline(markdown);
        headings.Select(h => h.Id).ShouldBe(outline.Select(o => o.Anchor));
        headings.Select(h => h.Id).Distinct().Count().ShouldBe(headings.Count);
        headings.Select(h => h.TextContent).ShouldBe(outline.Select(o => o.Text));
        outline.Select(o => o.Level).ShouldBe([1, 2, 2, 3]);
    }

    [Test]
    public async Task Render_WhenRawHtmlCarriesAnId_ShouldKeepIdsOnHeadingsOnly()
    {
        using var response = await RenderAsync(Api, "<p id=\"x\">hi</p>\n\n# Title\n\n**bold** `code`", null);

        var document = await new HtmlParser().ParseDocumentAsync((await response.Content.ReadFromJsonAsync<RenderedMarkdownDto>())!.Html);
        document.QuerySelectorAll("[id]").Select(e => e.LocalName).ShouldBe(["h1"]);
    }
}
