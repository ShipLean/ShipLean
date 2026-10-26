using Agoda.IoC.Core;
using Ganss.Xss;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Renderers.Html.Inlines;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Markdown;

namespace ShipLean.Application.Markdown;

// Renders markdown to HTML that is safe to put in the page. Raw HTML in the markdown is escaped to text by Markdig,
// and the output then goes through an allow-list sanitiser. Features that store markdown inject this class to render
// into their own DTOs. Images are written by MarkdownImageHtml; the only img the sanitiser keeps loads an attachment's
// content from this server, so a rendered text never fetches anything from elsewhere.
[RegisterSingleton]
public sealed class MarkdownRenderer
{
    public const int MaxLength = 100_000;

    private static readonly string[] AllowedTags =
    [
        "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre", "code", "em", "strong", "a",
        "ul", "ol", "li", "table", "thead", "tbody", "tr", "th", "td", "input", "figure", "figcaption", "img", "span",
    ];

    private const string ContentPrefix = "/api/attachments/";

    private const string ContentSuffix = "/content";

    private static readonly string[] HeadingTags = ["h1", "h2", "h3", "h4", "h5", "h6"];

    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    public RenderedMarkdownDto Render(string? markdown) => Render(markdown, null);

    // With a context the images are matched against the item's attachments; without one none are attached.
    public RenderedMarkdownDto Render(string? markdown, MarkdownImageContext? context) => Render(markdown, context, MaxLength);

    // For a stored Markdown file, whose size is limited in bytes by its caller, not by the typed-text character cap.
    public RenderedMarkdownDto RenderFile(string markdown, MarkdownImageContext? context = null) => Render(markdown, context, int.MaxValue);

    private RenderedMarkdownDto Render(string? markdown, MarkdownImageContext? context, int maxLength)
    {
        if (markdown is null)
        {
            throw ApiException.Validation("Markdown is required.");
        }

        if (markdown.Length > maxLength)
        {
            throw ApiException.Validation($"Markdown is too long: {markdown.Length:N0} characters, the limit is {MaxLength:N0}.");
        }

        var attachments = context?.Attachments ?? [];
        var images = MarkdownImages.Classify(markdown, attachments);
        var html = markdown.Length == 0 ? "" : _sanitizer.Sanitize(ToHtml(markdown, new MarkdownImageHtml(images, attachments)));
        return new RenderedMarkdownDto(markdown, html, images);
    }

    private static string ToHtml(string markdown, MarkdownImageHtml images)
    {
        var document = MarkdownPipelines.Parse(markdown);
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        MarkdownPipelines.Shared.Setup(renderer);
        renderer.ObjectRenderers.Replace<LinkInlineRenderer>(new MarkdownImageLinkRenderer(images));
        renderer.ObjectRenderers.Replace<ParagraphRenderer>(new MarkdownFigureParagraphRenderer(images));
        try
        {
            renderer.Render(document);
        }
        catch (ArgumentException)
        {
            // Markdig refuses to render elements nested past its depth limit (a few hundred emphasis markers).
            throw ApiException.Validation("Markdown is nested too deeply.");
        }

        writer.Flush();
        return writer.ToString();
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(AllowedTags);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(
            ["href", "title", "rel", "class", "style", "type", "checked", "disabled", "start", "id", "src", "alt", "data-md-image", "data-src"]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedCssProperties.Add("text-align");
        sanitizer.PostProcessNode += (_, e) =>
        {
            switch (e.Node)
            {
                case AngleSharp.Dom.IElement { LocalName: "a" } link when link.HasAttribute("href"):
                    link.SetAttribute("rel", "noopener noreferrer");
                    break;
                case AngleSharp.Dom.IElement { LocalName: "input" } input
                    when input.GetAttribute("type") != "checkbox" || !input.HasAttribute("disabled"):
                    input.Remove();
                    return;
                case AngleSharp.Dom.IElement { LocalName: "img" } image when !IsAttachmentContent(image.GetAttribute("src")):
                    image.Remove();
                    return;
            }

            if (e.Node is AngleSharp.Dom.IElement element)
            {
                if (element.HasAttribute("class"))
                {
                    KeepMarkdigClasses(element);
                }

                if (element.HasAttribute("id") && !HeadingTags.Contains(element.LocalName))
                {
                    element.RemoveAttribute("id");
                }

                KeepImageAttributes(element);
            }
        };
        return sanitizer;
    }

    // The img src was checked above; src and alt stay on img only, data-md-image only with the state its element shows,
    // and data-src only on an external placeholder, holding an absolute http(s) address the browser is never asked to load.
    private static void KeepImageAttributes(AngleSharp.Dom.IElement element)
    {
        if (element.LocalName != "img")
        {
            element.RemoveAttribute("src");
            element.RemoveAttribute("alt");
        }

        var state = element.GetAttribute("data-md-image");
        var stateAllowed = (element.LocalName, state) is ("figure", "attached") or ("span", "external") or ("span", "missing");
        if (state is not null && !stateAllowed)
        {
            element.RemoveAttribute("data-md-image");
        }

        var external = element.GetAttribute("data-src");
        if (external is not null && !(stateAllowed && state == "external" && IsHttpUrl(external)))
        {
            element.RemoveAttribute("data-src");
        }
    }

    private static bool IsAttachmentContent(string? src)
        => src is not null
            && src.Length == ContentPrefix.Length + 36 + ContentSuffix.Length
            && src.StartsWith(ContentPrefix, StringComparison.Ordinal)
            && src.EndsWith(ContentSuffix, StringComparison.Ordinal)
            && Guid.TryParseExact(src.AsSpan(ContentPrefix.Length, 36), "D", out _);

    private static bool IsHttpUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    // class is allowed only where Markdig emits it: language-* on code, the task-list classes on ul, li and input.
    private static void KeepMarkdigClasses(AngleSharp.Dom.IElement element)
    {
        var kept = element.ClassList.Where(c => element.LocalName switch
        {
            "code" => c.StartsWith("language-", StringComparison.Ordinal),
            "ul" => c == "contains-task-list",
            "li" => c == "task-list-item",
            "input" => c == "task-list-item-checkbox",
            _ => false,
        }).ToList();
        if (kept.Count == 0)
        {
            element.RemoveAttribute("class");
        }
        else
        {
            element.SetAttribute("class", string.Join(' ', kept));
        }
    }
}
