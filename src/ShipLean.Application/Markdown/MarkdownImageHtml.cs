using Markdig.Renderers;
using Markdig.Syntax.Inlines;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Markdown;

namespace ShipLean.Application.Markdown;

// Writes a Markdown image as the HTML pinned in DtoFixtures/markdown/rendered-images.json: an attached image is a figure whose
// img loads the attachment's content, captioned with its name; an external or missing one is a placeholder span that loads nothing.
internal sealed class MarkdownImageHtml(IReadOnlyList<MarkdownImageDto> images, IReadOnlyList<AttachmentDto> attachments)
{
    private readonly Dictionary<string, MarkdownImageDto> _bySrc = images.ToDictionary(i => i.Src, StringComparer.Ordinal);

    private readonly Dictionary<Guid, string> _names = attachments.ToDictionary(a => a.Id, a => a.Name);

    // images is MarkdownImages.Classify of the same text, so every image inline has its entry (keyed the same way, by Url).
    public bool IsFigure(LinkInline image) => Of(image).Status == MarkdownImageStatus.Attached;

    public void Write(HtmlRenderer renderer, LinkInline image)
    {
        var classified = Of(image);
        switch (classified.Status)
        {
            case MarkdownImageStatus.Attached:
                renderer.Write($"<figure data-md-image=\"attached\"><img src=\"/api/attachments/{classified.AttachmentId}/content\" alt=\"");
                WriteAlt(renderer, image);
                renderer.Write("\"><figcaption>");
                renderer.WriteEscape(_names[classified.AttachmentId!.Value]);
                renderer.Write("</figcaption></figure>");
                break;
            case MarkdownImageStatus.External:
                renderer.Write("<span data-md-image=\"external\" data-src=\"");
                renderer.WriteEscape(classified.Src);
                renderer.Write("\">External image not shown</span>");
                break;
            default:
                renderer.Write("<span data-md-image=\"missing\">");
                renderer.WriteEscape(classified.Src);
                renderer.Write("</span>");
                break;
        }
    }

    private MarkdownImageDto Of(LinkInline image) => _bySrc[image.Url ?? string.Empty];

    // The alt text is the image's description as plain text, the way Markdig writes it.
    private static void WriteAlt(HtmlRenderer renderer, LinkInline image)
    {
        var html = renderer.EnableHtmlForInline;
        renderer.EnableHtmlForInline = false;
        renderer.WriteChildren(image);
        renderer.EnableHtmlForInline = html;
    }
}
