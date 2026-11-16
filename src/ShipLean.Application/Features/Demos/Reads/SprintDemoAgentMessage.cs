using System.Text.RegularExpressions;
using Markdig;
using ShipLean.Application.Markdown;

namespace ShipLean.Application.Features.Demos.Reads;

// C5's quote of an agent's last words: the first paragraph of its last response or error, as plain text, at most MaxLength
// characters cut at a word. The only way raw agent text reaches the Demo tab. Pure.
internal static partial class SprintDemoAgentMessage
{
    public const int MaxLength = 280;

    private const string Ellipsis = "…";

    private const string LinkRemoved = "[link removed]";

    public static string? Excerpt(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        var paragraph = FirstParagraph(markdown);
        return paragraph.Length == 0 ? null : Cut(TokenLink().Replace(paragraph, LinkRemoved));
    }

    // The first top-level block that has any text, Markdown markers removed and whitespace collapsed.
    private static string FirstParagraph(string markdown)
    {
        Markdig.Syntax.MarkdownDocument document;
        try
        {
            document = Markdig.Markdown.Parse(markdown, MarkdownPipelines.Shared);
        }
        catch (ArgumentException)
        {
            // Markdig refuses input nested past its depth limit; the first raw paragraph is still plain enough to quote.
            return Collapse(BlankLine().Split(markdown.Trim())[0]);
        }

        return document
            .Select(block => Collapse(Markdig.Markdown.ToPlainText(markdown.Substring(block.Span.Start, block.Span.Length), MarkdownPipelines.Shared)))
            .FirstOrDefault(text => text.Length > 0) ?? "";
    }

    private static string Collapse(string text) => Whitespace().Replace(text, " ").Trim();

    private static string Cut(string text)
    {
        if (text.Length <= MaxLength)
        {
            return text;
        }

        var room = MaxLength - Ellipsis.Length;
        var space = text.LastIndexOf(' ', room);
        var head = space > 0 ? text[..space] : text[..room];
        return head.TrimEnd() + Ellipsis;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex BlankLine();

    // An upload or download link carries its token in the query; the quote never repeats one.
    [GeneratedRegex(@"\S*[?&]token=\S*", RegexOptions.IgnoreCase)]
    private static partial Regex TokenLink();
}
