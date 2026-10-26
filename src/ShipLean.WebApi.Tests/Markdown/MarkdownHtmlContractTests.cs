using System.Text.Json;
using System.Text.Json.Nodes;
using AngleSharp.Html.Parser;
using ShipLean.Application.Markdown;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Markdown;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Markdown;

// The image HTML the renderer must produce, pinned in DtoFixtures/markdown/rendered-images.json.
[TestFixture]
public class MarkdownHtmlContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static (IReadOnlyList<AttachmentDto> Attachments, RenderedMarkdownDto Expected) Fixture()
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "markdown", "rendered-images.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        return (json["attachments"].Deserialize<List<AttachmentDto>>(Options)!, json["rendered"].Deserialize<RenderedMarkdownDto>(Options)!);
    }

    [Test]
    public void Render_WhenFixtureAttachments_ShouldListTheFixtureImages()
    {
        var (attachments, expected) = Fixture();

        var rendered = new MarkdownRenderer().Render(expected.Markdown, new MarkdownImageContext(attachments));

        rendered.Images.ShouldBe(expected.Images);
    }

    [Test]
    public void Render_WhenHeadings_ShouldMatchTheFixtureIds()
    {
        var (attachments, expected) = Fixture();

        var rendered = new MarkdownRenderer().Render(expected.Markdown, new MarkdownImageContext(attachments));

        new HtmlParser().ParseDocument(rendered.Html).QuerySelector("h1")!.OuterHtml.ShouldBe(new HtmlParser().ParseDocument(expected.Html).QuerySelector("h1")!.OuterHtml);
    }

    // Red until A3: images are left out of the HTML for now.
    [Test]
    public void Render_WhenImages_ShouldMatchTheHtmlContract()
    {
        var (attachments, expected) = Fixture();

        var rendered = new MarkdownRenderer().Render(expected.Markdown, new MarkdownImageContext(attachments));

        static List<string> ImageMarkup(string html) => [.. new HtmlParser().ParseDocument(html).QuerySelectorAll("[data-md-image]").Select(e => e.OuterHtml)];
        ImageMarkup(rendered.Html).ShouldBe(ImageMarkup(expected.Html));
        ImageMarkup(expected.Html).Count.ShouldBe(3);
    }
}
