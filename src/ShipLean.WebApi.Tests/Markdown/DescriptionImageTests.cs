using System.Net;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Markdown;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

namespace ShipLean.WebApi.Tests.Markdown;

// Attached images in rendered descriptions: through POST api/markdown/render with a work item, and in the work item's own
// DescriptionHtml and ReproStepsHtml, which match the item's live image attachments.
[TestFixture]
public class DescriptionImageTests : ApiFixture
{
    private static readonly string BlobRoot = Path.Combine(Path.GetTempPath(), $"shiplean-blobs-{Guid.NewGuid():N}");

    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddSingleton<IBlobStore>(new LocalDiskBlobStore(BlobRoot));

    [OneTimeTearDown]
    public void DeleteBlobs() => Directory.Delete(BlobRoot, recursive: true);

    [Test]
    public async Task RenderEndpoint_WhenWorkItemGiven_ShouldReturnFigure()
    {
        var image = await UploadAsync(SeedIds.WorkItems.Ship90, "endpoint-figure.png");

        using var response = await Api.PostJsonAsync("api/markdown/render", new MarkdownRenderRequest("![Figure](endpoint-figure.png)", SeedIds.WorkItems.Ship90));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["html"]!.GetValue<string>();
        Figure(Parse(html)).ShouldBe(($"/api/attachments/{image.Id}/content", "endpoint-figure.png"));
    }

    [Test]
    public async Task GetWorkItem_WhenDescriptionHasAttachedImage_ShouldRenderFigure()
    {
        var id = await CreateStoryAsync("Picker mockup", "Before\n\n![Mockup](mockup.png)\n\nAfter");
        await UploadAsync(id, "mockup.png");
        var newest = await UploadAsync(id, "MOCKUP.png");

        var item = await AttachmentWire.GetAsync<WorkItemDto>(Api, $"api/work-items/{id}");

        Figure(Parse(item.DescriptionHtml)).ShouldBe(($"/api/attachments/{newest.Id}/content", "MOCKUP.png"));
    }

    [Test]
    public async Task GetWorkItem_WhenReproStepsHaveAttachedImage_ShouldRenderFigure()
    {
        var id = await CreateAsync("bug", "Crash on save", new JsonObject { ["reproSteps"] = new JsonObject { ["value"] = "1. Save\n\n![Crash](crash.png)" } });
        var image = await UploadAsync(id, "crash.png");

        var item = await AttachmentWire.GetAsync<WorkItemDto>(Api, $"api/work-items/{id}");

        Figure(Parse(item.ReproStepsHtml)).ShouldBe(($"/api/attachments/{image.Id}/content", "crash.png"));
    }

    [Test]
    public async Task GetWorkItem_WhenImageAttachmentArchived_ShouldRenderMissing()
    {
        var id = await CreateStoryAsync("Archived mockup", "![Gone](gone.png)");
        var image = await UploadAsync(id, "gone.png");
        (await Api.PostJsonAsync($"api/attachments/{image.Id}/archive")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var item = await AttachmentWire.GetAsync<WorkItemDto>(Api, $"api/work-items/{id}");

        var document = Parse(item.DescriptionHtml);
        document.QuerySelectorAll("img").ShouldBeEmpty();
        var placeholder = document.QuerySelector("[data-md-image]")!;
        placeholder.GetAttribute("data-md-image").ShouldBe("missing");
        placeholder.TextContent.ShouldBe("gone.png");
    }

    private Task<Contracts.Attachments.AttachmentDto> UploadAsync(Guid workItemId, string name)
        => AttachmentUploads.UploadAsync(Client, $"api/work-items/{workItemId}/attachments", name, "image/png", [1, 2, 3]);

    private Task<Guid> CreateStoryAsync(string title, string description)
        => CreateAsync("story", title, new JsonObject { ["description"] = new JsonObject { ["value"] = description } });

    private async Task<Guid> CreateAsync(string type, string title, JsonObject changes)
    {
        using var response = await Api.PostJsonAsync("api/work-items", new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = type,
            ["title"] = title,
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = changes,
        });
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
        return JsonNode.Parse(body)!["id"]!.GetValue<Guid>();
    }

    private static IDocument Parse(string html) => new HtmlParser().ParseDocument(html);

    // The img src and the caption of the one attached figure.
    private static (string? Src, string Caption) Figure(IDocument document)
    {
        var figure = document.QuerySelectorAll("figure[data-md-image=attached]").ShouldHaveSingleItem();
        return (figure.QuerySelector("img")?.GetAttribute("src"), figure.QuerySelector("figcaption")?.TextContent ?? "");
    }
}
