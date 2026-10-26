using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;

namespace ShipLean.Application.Markdown;

// Markdig's link renderer, with images written by MarkdownImageHtml instead of as a plain img. Inside another image's alt
// text (HTML off) an image is only its text, as Markdig writes it.
internal sealed class MarkdownImageLinkRenderer(MarkdownImageHtml images) : LinkInlineRenderer
{
    protected override void Write(HtmlRenderer renderer, LinkInline link)
    {
        if (link.IsImage && renderer.EnableHtmlForInline)
        {
            images.Write(renderer, link);
            return;
        }

        base.Write(renderer, link);
    }
}
