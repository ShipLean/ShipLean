using System.Net;
using System.Net.Http.Json;
using AngleSharp.Html.Parser;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Markdown;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Markdown;

[TestFixture]
public class MarkdownRenderTests : ApiFixture
{
    private async Task<RenderedMarkdownDto> RenderAsync(string markdown)
    {
        var response = await Client.PostAsJsonAsync("/api/markdown/render", new MarkdownRenderRequest(markdown));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<RenderedMarkdownDto>())!;
    }

    private static AngleSharp.Dom.IDocument Parse(string html) => new HtmlParser().ParseDocument(html);

    [Test]
    public async Task Markdown_WhenScript_ShouldBeStripped()
    {
        var result = await RenderAsync("""
            hello <script>alert(1)</script> <img src=x onerror="alert(2)">

            <div onclick="alert(3)">raw</div>

            [click](javascript:alert(4)) <b onmouseover="x()">bold</b>
            """);

        var document = Parse(result.Html);
        var all = document.All.ToList();
        all.ShouldNotBeEmpty();
        all.Any(e => e.LocalName == "script").ShouldBeFalse();
        all.SelectMany(e => e.Attributes).Any(a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    [Test]
    public async Task Markdown_WhenRendered_ShouldReturnBothMarkdownAndHtml()
    {
        const string markdown = """
            # Title

            - one
            - two

            1. first
            2. second

            | a | b |
            |---|---|
            | 1 | 2 |

            - [x] done
            - [ ] open

            ```csharp
            var x = 1;
            ```
            """;

        var result = await RenderAsync(markdown);

        result.Markdown.ShouldBe(markdown);
        var document = Parse(result.Html);
        document.QuerySelectorAll("h1").Length.ShouldBe(1);
        document.QuerySelectorAll("ul > li").Length.ShouldBe(4);
        document.QuerySelectorAll("ol > li").Length.ShouldBe(2);
        document.QuerySelectorAll("table th").Length.ShouldBe(2);
        document.QuerySelectorAll("table td").Length.ShouldBe(2);
        var boxes = document.QuerySelectorAll("input[type=checkbox]");
        boxes.Length.ShouldBe(2);
        boxes.Count(b => b.HasAttribute("checked")).ShouldBe(1);
        boxes.All(b => b.HasAttribute("disabled")).ShouldBeTrue();
        document.QuerySelector("pre > code")!.TextContent.ShouldContain("var x = 1;");
    }

    [Test]
    public async Task Markdown_WhenEmpty_ShouldRenderEmptyHtml()
    {
        var result = await RenderAsync("");

        result.Html.ShouldBe("");
    }

    [Test]
    public async Task Markdown_WhenLinkRendered_ShouldOpenSafely()
    {
        var result = await RenderAsync("[bad](javascript:alert(1)) [good](https://example.com/a) https://example.org/b <javascript:alert(1)>");

        var document = Parse(result.Html);
        document.QuerySelectorAll("a").Any(a => (a.GetAttribute("href") ?? "").Contains("javascript", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        var hrefs = document.QuerySelectorAll("a[href]");
        hrefs.Select(a => a.GetAttribute("href")).ShouldBe(["https://example.com/a", "https://example.org/b"]);
        hrefs.All(a => a.GetAttribute("rel") == "noopener noreferrer").ShouldBeTrue();
    }

    [Test]
    public async Task Markdown_WhenTooLong_ShouldBeValidation()
    {
        var response = await Client.PostAsJsonAsync("/api/markdown/render", new MarkdownRenderRequest(new string('a', 100_001)));

        (await response.ReadApiErrorAsync(HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [TestCase(">")]
    [TestCase("*")]
    public async Task Markdown_WhenNestedTooDeeply_ShouldBeValidation(string marker)
    {
        var markdown = marker == ">" ? new string('>', 200) + " x" : new string('*', 1000) + "x" + new string('*', 1000);

        var response = await Client.PostAsJsonAsync("/api/markdown/render", new MarkdownRenderRequest(markdown));

        (await response.ReadApiErrorAsync(HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
