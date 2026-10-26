using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace ShipLean.Application.Features.Links.OpenGraph;

// What a page says about itself: og:title (else <title>), og:description (else the description meta), og:site_name, og:image
// and the icon link (else /favicon.ico), the last two resolved against the page's address and kept only when they are http/https. Text is trimmed, whitespace
// collapsed and capped, so a hostile page can't store a megabyte of title.
public sealed partial record OpenGraphPage(string? Title, string? Description, string? Site, Uri? Image, Uri? Icon)
{
    public const int MaxTitle = 300;
    public const int MaxDescription = 1000;
    public const int MaxSite = 100;

    public static OpenGraphPage Parse(byte[] html, Uri address)
    {
        using var document = new HtmlParser().ParseDocument(new MemoryStream(html, writable: false));
        var title = Text(Meta(document, "og:title") ?? document.Title, MaxTitle);
        var description = Text(Meta(document, "og:description") ?? Named(document, "description"), MaxDescription);
        var site = Text(Meta(document, "og:site_name"), MaxSite);
        var image = Resolve(address, Meta(document, "og:image"));
        var icon = Resolve(address, document.QuerySelectorAll("link[rel][href]")
            .FirstOrDefault(link => link.GetAttribute("rel")!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("icon", StringComparer.OrdinalIgnoreCase))
            ?.GetAttribute("href") ?? "/favicon.ico");
        return new OpenGraphPage(title, description, site, image, icon);
    }

    private static string? Meta(IDocument document, string property)
        => document.QuerySelectorAll("meta[property][content]")
            .FirstOrDefault(meta => string.Equals(meta.GetAttribute("property"), property, StringComparison.OrdinalIgnoreCase))
            ?.GetAttribute("content");

    private static string? Named(IDocument document, string name)
        => document.QuerySelectorAll("meta[name][content]")
            .FirstOrDefault(meta => string.Equals(meta.GetAttribute("name"), name, StringComparison.OrdinalIgnoreCase))
            ?.GetAttribute("content");

    private static string? Text(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var text = Whitespace().Replace(value, " ").Trim();
        if (text.Length <= max)
        {
            return text;
        }
        var cut = char.IsHighSurrogate(text[max - 1]) ? max - 1 : max;
        return text[..cut].TrimEnd() + "…";
    }

    // Uri reads "/og.png" as an absolute file: address on Linux, so root- and scheme-relative references are joined by hand.
    private static Uri? Resolve(Uri page, string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }
        var text = reference.Trim();
        Uri? resolved;
        if (text.StartsWith("//", StringComparison.Ordinal))
        {
            Uri.TryCreate($"{page.Scheme}:{text}", UriKind.Absolute, out resolved);
        }
        else if (text.StartsWith('/'))
        {
            Uri.TryCreate(page.GetLeftPart(UriPartial.Authority) + text, UriKind.Absolute, out resolved);
        }
        else
        {
            Uri.TryCreate(page, text, out resolved);
        }
        return resolved is not null && (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps) ? resolved : null;
    }

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Whitespace();
}
