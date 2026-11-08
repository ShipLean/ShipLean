using System.Net;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ShipLean.Application.Features.IssueIntake.Proposal;

// The last check (D17) on every agent-written text before it is posted on GitHub. The caller builds one per post with the
// instance's base URLs and the names of the project's members, then calls Clean with the issue author's login.
// GitHub renders HTML, so the text is parsed the way GitHub's CommonMark parser reads it (HTML on, autolinks on), not the way
// ShipLean's own renderer does (HTML off). Entities are decoded first so an encoded host, email or tag can't slip past.
public sealed partial class PublicTextGuard
{
    public const int MaxLength = 60_000;

    private const string CutNote = "\n\n(The rest was cut: posts are limited to 60,000 characters.)";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);

    private static readonly MarkdownPipeline GitHubLike = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UsePreciseSourceLocation()
        .Build();

    private readonly IReadOnlyList<Regex> hosts;
    private readonly IReadOnlyList<(string Name, Regex Pattern)> names;

    public PublicTextGuard(IReadOnlyCollection<string> instanceBaseUrls, IReadOnlyCollection<string> memberNames)
    {
        hosts = instanceBaseUrls
            .Select(HostOf)
            .Where(h => h.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(h => new Regex($"(?<![a-z0-9.-]){Regex.Escape(h)}(?![a-z0-9-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout))
            .ToList();
        names = memberNames
            .Select(n => n.Trim())
            .Where(n => n.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length)
            .Select(n => (n, NamePattern(n)))
            .ToList();
    }

    public GuardResult Clean(string markdown, string issueAuthor)
    {
        var removed = new List<string>();
        var text = Cap(markdown, removed);
        text = DecodeEntities(text, removed);
        text = RemoveMarkup(text, removed);
        text = Regex.Replace(text, @"\S+", m => Remove(m.Value, PointsToInstance(m.Value), "instance link", removed), RegexOptions.None, MatchTimeout);
        text = EmailPattern().Replace(text, m => Remove(m.Value, true, "email", removed));
        foreach (var (name, pattern) in names)
        {
            text = pattern.Replace(text, m => Remove(m.Value, true, $"member name {name}", removed));
        }

        text = NeutraliseMentions(text, issueAuthor, m => $"`{m.Value.TrimStart('\\')}`", removed);
        text = NeutraliseMentions(text, issueAuthor, m => m.Value.TrimStart('\\').Insert(1, "\u200B"), removed);
        return new GuardResult(Cap(text, removed), removed);
    }

    private static string HostOf(string baseUrl) =>
        Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.Host : baseUrl.Trim().Trim('/');

    private static Regex NamePattern(string name)
    {
        var parts = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape);
        return new Regex($@"(?<![\p{{L}}\p{{N}}]){string.Join(@"\s+", parts)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
    }

    private static string Cap(string text, List<string> removed)
    {
        if (text.Length <= MaxLength)
        {
            return text;
        }

        var keep = MaxLength - CutNote.Length;
        if (char.IsHighSurrogate(text[keep - 1]))
        {
            keep--;
        }

        removed.Add($"cut: {text.Length - keep} characters past {MaxLength}");
        return text[..keep] + CutNote;
    }

    // Decoded until it stops changing, so a double-encoded entity is decoded too.
    private static string DecodeEntities(string text, List<string> removed)
    {
        var decoded = text;
        string before;
        do
        {
            before = decoded;
            decoded = WebUtility.HtmlDecode(before);
        }
        while (!string.Equals(decoded, before, StringComparison.Ordinal));

        if (!string.Equals(decoded, text, StringComparison.Ordinal))
        {
            removed.Add("entities: decoded HTML entities");
        }

        return decoded;
    }

    // Raw HTML (blocks, tags and comments) and links to the instance, repeated until a parse finds none: removing one tag can
    // join the text around it into another (<<b>script>). Every pass shortens the text, so the loop ends.
    private string RemoveMarkup(string text, List<string> removed)
    {
        while (true)
        {
            var edits = new List<Edit>();
            foreach (var node in Markdig.Markdown.Parse(text, GitHubLike).Descendants())
            {
                var edit = MarkupEdit(node, text);
                if (edit is not null)
                {
                    edits.Add(edit);
                }
            }

            if (edits.Count == 0)
            {
                return text;
            }

            text = Apply(text, edits, removed);
        }
    }

    private Edit? MarkupEdit(MarkdownObject node, string text) => node switch
    {
        HtmlBlock or HtmlInline => new Edit(node.Span.Start, node.Span.End + 1, string.Empty, "HTML"),
        LinkInline link when PointsToInstance(link.Url) => new Edit(link.Span.Start, link.Span.End + 1, LabelOf(link, text), "instance link"),
        AutolinkInline auto when PointsToInstance(auto.Url) => new Edit(auto.Span.Start, auto.Span.End + 1, string.Empty, "instance link"),
        LinkReferenceDefinition definition when PointsToInstance(definition.Url) => new Edit(definition.Span.Start, definition.Span.End + 1, string.Empty, "instance link"),
        _ => null,
    };

    // A bare autolink's label is its URL, so it goes whole.
    private static string LabelOf(LinkInline link, string text) =>
        link.IsAutoLink || link.FirstChild is null || link.LastChild is null
            ? string.Empty
            : text[link.FirstChild.Span.Start..(link.LastChild.Span.End + 1)];

    private bool PointsToInstance(string? url)
    {
        if (string.IsNullOrEmpty(url) || hosts.Count == 0)
        {
            return false;
        }

        var decoded = url;
        string before;
        do
        {
            before = decoded;
            decoded = Uri.UnescapeDataString(before);
        }
        while (!string.Equals(decoded, before, StringComparison.Ordinal));

        return hosts.Any(h => h.IsMatch(decoded));
    }

    // Mentions outside code spans and code blocks, other than the issue author's (a team mention is never theirs). The first
    // call wraps them in backticks; a stray backtick in the text can pair with one of those and leave a mention outside code,
    // so the second call breaks whatever is still live with a zero-width space after the @.
    private static string NeutraliseMentions(string text, string issueAuthor, Func<Match, string> neutralise, List<string> removed)
    {
        var code = Markdig.Markdown.Parse(text, GitHubLike).Descendants()
            .Where(n => n is CodeInline or CodeBlock)
            .Select(n => (Start: n.Span.Start, End: n.Span.End + 1))
            .ToList();
        var edits = MentionPattern().Matches(text)
            .Where(m => !code.Any(c => m.Index < c.End && c.Start < m.Index + m.Length))
            .Where(m => !string.Equals(m.Value.TrimStart('\\'), "@" + issueAuthor, StringComparison.OrdinalIgnoreCase))
            .Select(m => new Edit(m.Index, m.Index + m.Length, neutralise(m), "mention"))
            .ToList();
        return Apply(text, edits, removed);
    }

    // Applies the outermost edits (an edit inside another waits for the next pass), from the end so offsets hold.
    private static string Apply(string text, List<Edit> edits, List<string> removed)
    {
        var kept = new List<Edit>();
        foreach (var edit in edits.OrderBy(e => e.Start).ThenByDescending(e => e.End))
        {
            if (kept.Count == 0 || edit.Start >= kept[^1].End)
            {
                kept.Add(edit);
            }
        }

        foreach (var edit in Enumerable.Reverse(kept))
        {
            removed.Add($"{edit.Kind}: {Snippet(text[edit.Start..edit.End])}");
            text = string.Concat(text.AsSpan(0, edit.Start), edit.Replacement, text.AsSpan(edit.End));
        }

        return text;
    }

    private static string Remove(string value, bool remove, string kind, List<string> removed)
    {
        if (!remove)
        {
            return value;
        }

        removed.Add($"{kind}: {Snippet(value)}");
        return string.Empty;
    }

    private static string Snippet(string value)
    {
        var flat = string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= 80 ? flat : flat[..80] + "…";
    }

    // name@host.tld, name @ host.tld, and name [at] host [dot] tld with (), [] or {}.
    [GeneratedRegex(@"[a-z0-9._%+-]+(?:@|\s+@\s+|\s*[\[({]\s*at\s*[\])}]\s*)[a-z0-9-]+(?:(?:\.|\s*[\[({]\s*dot\s*[\])}]\s*)[a-z0-9-]+)+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex EmailPattern();

    // A GitHub login (letters, digits, single hyphens, up to 39) or org/team, not inside a word, a path or an email.
    [GeneratedRegex(@"(?<![A-Za-z0-9_/.@])\\?@[A-Za-z0-9](?:[A-Za-z0-9]|-(?=[A-Za-z0-9])){0,38}(?:/[A-Za-z0-9_-]+)?(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex MentionPattern();

    private sealed record Edit(int Start, int End, string Replacement, string Kind);
}
