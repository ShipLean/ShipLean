using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Markdown;

namespace ShipLean.Application.Markdown;

// The images a Markdown text refers to, once per distinct source, in order of first use. An absolute http(s) URL is external;
// a bare name (no folder) matching an image attachment of the item, ignoring case, is attached (the newest if several); the rest is missing.
public static class MarkdownImages
{
    public static IReadOnlyList<MarkdownImageDto> Classify(string markdown, IReadOnlyList<AttachmentDto> attachments)
    {
        var images = attachments.Where(a => a.Kind == AttachmentKind.Image).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<MarkdownImageDto>();
        foreach (var link in MarkdownPipelines.Parse(markdown).Descendants<LinkInline>().Where(l => l.IsImage))
        {
            var src = link.Url ?? string.Empty;
            if (seen.Add(src))
            {
                result.Add(ClassifyOne(src, images));
            }
        }

        return result;
    }

    private static MarkdownImageDto ClassifyOne(string src, List<AttachmentDto> images)
    {
        if (Uri.TryCreate(src, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
        {
            return new MarkdownImageDto(src, MarkdownImageStatus.External, null);
        }

        var match = src.Length > 0 && src.IndexOfAny(['/', '\\']) < 0
            ? images.LastOrDefault(a => a.Name.Equals(src, StringComparison.OrdinalIgnoreCase))
            : null;
        return match is null
            ? new MarkdownImageDto(src, MarkdownImageStatus.Missing, null)
            : new MarkdownImageDto(src, MarkdownImageStatus.Attached, match.Id);
    }
}
