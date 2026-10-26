using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ShipLean.Application.Markdown;
using ShipLean.Contracts.Attachments;

namespace ShipLean.WebApi.Tests.Markdown;

// The image HTML the renderer writes: a figure for an attached image, a placeholder that loads nothing for the rest, and no
// img the Markdown text itself can point somewhere.
[TestFixture]
public class MarkdownImageHtmlTests
{
    private static readonly AttachmentDto Picker = new(
        Guid.NewGuid(), Guid.NewGuid(), "Card-Picker.png", "image/png", 3, null, DateTimeOffset.UnixEpoch, "1 Jan", "", AttachmentKind.Image, 0, null, null);

    private static IDocument Render(string markdown)
        => new HtmlParser().ParseDocument(new MarkdownRenderer().Render(markdown, new MarkdownImageContext([Picker])).Html);

    [Test]
    public void Render_WhenImageAttached_ShouldEmitFigureWithContentUrl()
    {
        var html = new MarkdownRenderer().Render("![The \"saved\" picker](card-picker.png)\n", new MarkdownImageContext([Picker])).Html;

        var figure = new HtmlParser().ParseDocument(html).Body!.Children.ShouldHaveSingleItem();
        figure.LocalName.ShouldBe("figure");
        figure.GetAttribute("data-md-image").ShouldBe("attached");
        var image = figure.QuerySelector("img")!;
        image.GetAttribute("src").ShouldBe($"/api/attachments/{Picker.Id}/content");
        image.GetAttribute("alt").ShouldBe("The \"saved\" picker");
        figure.QuerySelector("figcaption")!.TextContent.ShouldBe("Card-Picker.png");
    }

    [Test]
    public void Render_WhenExternalImage_ShouldEmitPlaceholderWithoutImg()
    {
        var document = Render("See ![Logo](https://example.com/logo.png?size=2&dark=1) here");

        document.QuerySelectorAll("img").ShouldBeEmpty();
        var placeholder = document.QuerySelector("p > span[data-md-image]")!;
        placeholder.GetAttribute("data-md-image").ShouldBe("external");
        placeholder.GetAttribute("data-src").ShouldBe("https://example.com/logo.png?size=2&dark=1");
        placeholder.TextContent.ShouldBe("External image not shown");
    }

    [TestCase("<img src=\"https://example.com/x.png\">")]
    [TestCase("<img src=\"/api/attachments/00000009-0000-4000-8001-000000000001/content\">")]
    [TestCase("<figure data-md-image=\"attached\"><img src=\"javascript:alert(1)\"></figure>")]
    [TestCase("<span data-md-image=\"external\" data-src=\"javascript:alert(1)\">x</span>")]
    [TestCase("![x](javascript:alert(1))")]
    [TestCase("![x](<javascript:alert(1)> \"t\")")]
    [TestCase("[![x](card-picker.png)](javascript:alert(1))")]
    public void Render_WhenImgSrcForged_ShouldStripIt(string markdown)
    {
        var document = Render(markdown);

        document.QuerySelectorAll("img").Where(i => i.GetAttribute("src") != $"/api/attachments/{Picker.Id}/content").ShouldBeEmpty();
        document.QuerySelectorAll("[data-src]").ShouldBeEmpty();
        document.All.SelectMany(e => e.Attributes).Select(a => a.Value)
            .ShouldNotContain(v => v.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase));
    }
}
