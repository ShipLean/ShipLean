using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ShipLean.Application.Markdown;

// A paragraph that is only an attached image is written as the bare figure: a figure can't sit inside a p, and the
// browser (and the sanitiser's parser) would split it into empty paragraphs around the figure.
internal sealed class MarkdownFigureParagraphRenderer(MarkdownImageHtml images) : ParagraphRenderer
{
    protected override void Write(HtmlRenderer renderer, ParagraphBlock obj)
    {
        if (obj.Inline?.FirstChild is LinkInline { IsImage: true, NextSibling: null } image && renderer.EnableHtmlForBlock && images.IsFigure(image))
        {
            renderer.EnsureLine();
            renderer.WriteLeafInline(obj);
            renderer.EnsureLine();
            return;
        }

        base.Write(renderer, obj);
    }
}
