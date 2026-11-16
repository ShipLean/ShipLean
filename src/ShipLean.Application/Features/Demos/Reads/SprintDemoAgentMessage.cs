using System.Globalization;
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

    private const string Removed = "[removed]";

    public static string? Excerpt(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return null;
        }

        // Redacted before the parse (so Markdown cannot split a secret) and after it (so a secret the parse assembled is caught).
        var paragraph = Redact(Collapse(HtmlTag().Replace(FirstParagraph(Redact(markdown)), " ")));
        return paragraph.Length == 0 ? null : Cut(paragraph);
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
            // Markdig refuses input nested past its depth limit; take the first blank-line paragraph and strip it to plain text ourselves.
            return Collapse(StripMarkup(BlankLine().Split(markdown.Trim())[0]));
        }

        return document
            .Select(block => Collapse(Markdig.Markdown.ToPlainText(markdown.Substring(block.Span.Start, block.Span.Length), MarkdownPipelines.Shared)))
            .FirstOrDefault(text => text.Length > 0) ?? "";
    }

    private static string Collapse(string text) => Whitespace().Replace(text, " ").Trim();

    // Depth-safe plain text: no parser, so nothing can refuse it. Tags, images and links keep only their text; Markdown markers go.
    private static string StripMarkup(string text)
    {
        var plain = HtmlTag().Replace(text, " ");
        plain = MarkdownLink().Replace(plain, "$1");
        return MarkdownMarker().Replace(plain, "");
    }

    // The quote never repeats a credential: each match of a secret pattern becomes [removed].
    private static string Redact(string text)
    {
        foreach (var pattern in Secrets)
        {
            text = pattern.Replace(text, Removed);
        }

        return text;
    }

    // Cut at a text-element boundary (never inside a surrogate pair or a combining sequence), then at a word, so the
    // result with its ellipsis is at most MaxLength UTF-16 characters.
    private static string Cut(string text)
    {
        if (text.Length <= MaxLength)
        {
            return text;
        }

        var room = MaxLength - Ellipsis.Length;
        var end = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext() && elements.ElementIndex + ((string)elements.Current).Length <= room)
        {
            end = elements.ElementIndex + ((string)elements.Current).Length;
        }

        var head = text[..end];
        var space = head.LastIndexOf(' ');
        if (space > 0 && text[end] != ' ')
        {
            head = head[..space];
        }

        return head.TrimEnd() + Ellipsis;
    }

    // The secret shapes ShipLean already refuses to post on GitHub: copied from PublicTextGuard's NamedSecrets (its Clean needs
    // an instance's hosts and member names and never redacts, so it cannot be called here), plus bearer and basic credentials and
    // upload or download links with a query string.
    private static readonly Regex[] Secrets =
    [
        TokenParameter(),
        UploadLink(),
        GitHubToken(),
        AwsAccessKeyId(),
        AwsSecretKey(),
        SlackToken(),
        SlackWebhook(),
        AnthropicKey(),
        SkKey(),
        JsonWebToken(),
        PrivateKeyHeader(),
        BearerOrBasic(),
    ];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex BlankLine();

    [GeneratedRegex(@"\S*[?&]token=\S*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex TokenParameter();

    [GeneratedRegex(@"\S*/api/(?:uploads|downloads)\S*\?\S*", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex UploadLink();

    [GeneratedRegex("gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{20,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex GitHubToken();

    [GeneratedRegex("(?<![A-Za-z0-9])(?:AKIA|ASIA)[A-Z0-9]{16}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex AwsAccessKeyId();

    [GeneratedRegex(@"(?:aws_?)?secret_?access_?key[""']?\s*[:=]\s*[""']?[A-Za-z0-9/+=]{40}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex AwsSecretKey();

    [GeneratedRegex("xox[abprs]-[A-Za-z0-9-]{10,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex SlackToken();

    [GeneratedRegex(@"hooks\.slack\.com/services/[A-Za-z0-9/]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex SlackWebhook();

    [GeneratedRegex("(?<![A-Za-z0-9_-])sk-ant-[A-Za-z0-9_-]{20,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex AnthropicKey();

    [GeneratedRegex("(?<![A-Za-z0-9_-])sk-[A-Za-z0-9_-]{32,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex SkKey();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex JsonWebToken();

    [GeneratedRegex(@"-----\s*BEGIN[A-Z0-9 ]*PRIVATE ?KEY[A-Z ]*-----", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex PrivateKeyHeader();

    [GeneratedRegex(@"\b(?:Bearer|Basic)\s+[A-Za-z0-9._~+/=-]{8,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex BearerOrBasic();

    [GeneratedRegex("<[^<>]*>", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"!?\[([^\]]*)\]\([^)]*\)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"[*`~#>]+|(?<![A-Za-z0-9])_+|_+(?![A-Za-z0-9])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex MarkdownMarker();
}
