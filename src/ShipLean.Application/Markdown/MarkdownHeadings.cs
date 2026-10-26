using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using ShipLean.Contracts.Markdown;

namespace ShipLean.Application.Markdown;

// The headings of a Markdown text, with the ids the renderer puts on them (same pipeline, so they always match).
public static class MarkdownHeadings
{
    public static IReadOnlyList<MarkdownOutlineEntryDto> Outline(string markdown)
        => [.. MarkdownPipelines.Parse(markdown).Descendants<HeadingBlock>()
            .Select(h => new MarkdownOutlineEntryDto(h.Level, TextOf(h), h.TryGetAttributes()?.Id ?? string.Empty))];

    private static string TextOf(HeadingBlock heading)
        => heading.Inline is null
            ? string.Empty
            : string.Concat(heading.Inline.Descendants().Select(i => i switch
            {
                LiteralInline literal => literal.Content.ToString(),
                CodeInline code => code.Content,
                _ => string.Empty,
            }));
}
