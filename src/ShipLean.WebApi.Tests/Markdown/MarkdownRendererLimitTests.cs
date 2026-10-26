using ShipLean.Application.Markdown;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Tests.Markdown;

// The 100,000-character cap is for typed Markdown; a stored file rendered through RenderFile is limited by its caller.
[TestFixture]
public class MarkdownRendererLimitTests
{
    private static readonly string LongMarkdown = new('a', MarkdownRenderer.MaxLength + 1);

    [Test]
    public void Render_WhenOverTheTypedLimit_ShouldBeValidation()
    {
        var thrown = Should.Throw<ApiException>(() => new MarkdownRenderer().Render(LongMarkdown));

        thrown.Message.ShouldContain("Markdown is too long");
    }

    [Test]
    public void RenderFile_WhenOverTheTypedLimit_ShouldRender()
    {
        var rendered = new MarkdownRenderer().RenderFile(LongMarkdown);

        rendered.Html.ShouldBe($"<p>{LongMarkdown}</p>\n");
    }

    [Test]
    public void RenderFile_WhenNestedTooDeeply_ShouldBeValidation()
    {
        var thrown = Should.Throw<ApiException>(() => new MarkdownRenderer().RenderFile(new string('>', 5_000) + " x"));

        thrown.Message.ShouldContain("nested too deeply");
    }
}
