using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace ShipLean.Application.Features.IssueIntake.Proposal;

// The last check (D17) on every agent-written text before it is posted on GitHub. The caller builds one per post with the
// instance's base URLs and the names of the project's members, then calls Clean with the issue author's login.
// GitHub renders HTML, so the text is parsed the way GitHub's CommonMark parser reads it (HTML on, autolinks on), not the way
// ShipLean's own renderer does (HTML off). Entities are decoded first so an encoded host, email or tag can't slip past.
// Remove, then verify, then fail closed: the removal passes and the cap repeat until the text stops changing (so a cap can't
// undo a removal, and Clean is a fixed point), then the exact output is checked again, raw and as rendered plain text. If the
// check finds anything, or the input is too big or too deep to read, the result is unsafe and its text is empty.
public sealed partial class PublicTextGuard
{
    public const int MaxLength = 60_000;

    // Input past this is refused, not cut: no agent response is this long.
    public const int MaxInput = 256 * 1024;

    private const int MaxRounds = 8;
    private const int MaxMarkupPasses = 16;
    private const int MaxDecodePasses = 8;
    private const string OutboxMarker = "shiplean:outbox";
    private const string CutNote = "\n\n(The rest was cut: posts are limited to 60,000 characters.)";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);
    private static readonly IdnMapping Idn = new();

    private static readonly MarkdownPipeline GitHubLike = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseAutoLinks()
        .UsePreciseSourceLocation()
        .Build();

    private readonly IReadOnlyList<string> asciiHosts;
    private readonly IReadOnlyList<IPAddress> addresses;
    private readonly IReadOnlyList<Regex> hosts;
    private readonly IReadOnlyList<(string Name, Regex Pattern)> names;

    public PublicTextGuard(IReadOnlyCollection<string> instanceBaseUrls, IReadOnlyCollection<string> memberNames)
    {
        asciiHosts = instanceBaseUrls.Select(HostOf).Where(h => h.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        addresses = asciiHosts
            .Select(h => IPAddress.TryParse(h.Trim('[', ']'), out var ip) ? Canonical(ip) : null)
            .OfType<IPAddress>()
            .ToList();

        // The ASCII and Unicode spellings of each host; the lookbehind lets a subdomain match, but not another host that ends
        // with the same letters (notprod.<host>), and the lookahead rejects a longer host that starts with it (<host>.example.com).
        hosts = asciiHosts
            .SelectMany(h => new[] { h, UnicodeOf(h) })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(h => new Regex($"(?<![a-z0-9-]){Regex.Escape(h)}(?![a-z0-9-]|\\.[a-z0-9-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout))
            .ToList();
        names = memberNames
            .Select(n => Whitespace().Replace(n.Trim(), " "))
            .Where(n => n.Contains(' ')) // a single word ("Mark") is an ordinary word, so only full names are matched
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(n => n.Length)
            .Select(n => (n, NamePattern(n)))
            .ToList();
    }

    public GuardResult Clean(string markdown, string issueAuthor)
    {
        var removed = new List<string>();
        if (markdown.Length > MaxInput)
        {
            return Unsafe($"input is {markdown.Length} characters, past {MaxInput}", removed);
        }

        try
        {
            var text = Cap(markdown, removed);
            for (var round = 0; round < MaxRounds; round++)
            {
                var next = Removal(text, issueAuthor, removed);
                if (next is null)
                {
                    return Unsafe("the text is nested or encoded too many times to clean", removed);
                }

                next = Cap(next, removed);
                if (string.Equals(next, text, StringComparison.Ordinal))
                {
                    var problem = Verify(text, issueAuthor);
                    return problem is null ? new GuardResult(true, text, removed) : Unsafe(problem, removed);
                }

                text = next;
            }

            return Unsafe($"the text still changed after {MaxRounds} rounds", removed);
        }
        catch (ArgumentException e)
        {
            // Markdig refuses Markdown nested past its depth limit.
            return Unsafe($"unreadable: {e.Message}", removed);
        }
        catch (RegexMatchTimeoutException)
        {
            return Unsafe("a pattern took too long", removed);
        }
    }

    private static GuardResult Unsafe(string reason, List<string> removed)
    {
        removed.Add($"unsafe: {reason}");
        return new GuardResult(false, string.Empty, removed);
    }

    // One round of the removal passes; null when a pass can't settle within its bound.
    private string? Removal(string text, string issueAuthor, List<string> removed)
    {
        var decoded = DecodeEntities(text, removed);
        var cleaned = decoded is null ? null : RemoveMarkup(decoded, removed);
        if (cleaned is null)
        {
            return null;
        }

        cleaned = Regex.Replace(cleaned, @"\S+", m => Remove(m.Value, PointsToInstance(m.Value), "instance link", removed), RegexOptions.None, MatchTimeout);
        cleaned = EmailPattern().Replace(cleaned, m => Remove(m.Value, true, "email", removed));
        foreach (var (name, pattern) in names)
        {
            cleaned = pattern.Replace(cleaned, m => Remove(m.Value, true, $"member name {name}", removed));
        }

        cleaned = NeutraliseMentions(cleaned, issueAuthor, m => $"`{m.Value.TrimStart('\\')}`", removed);
        return NeutraliseMentions(cleaned, issueAuthor, m => m.Value.TrimStart('\\').Insert(1, "\u200B"), removed);
    }

    // The check on the exact output: null when it may be posted, otherwise what was found.
    private string? Verify(string text, string issueAuthor)
    {
        var raw = Normalise(text);
        if (raw.Contains(OutboxMarker, StringComparison.OrdinalIgnoreCase))
        {
            return "an outbox marker";
        }

        var mention = LiveMentions(text, issueAuthor).FirstOrDefault();
        if (mention is not null)
        {
            return $"live mention {mention.Value}";
        }

        foreach (var view in new[] { raw, Normalise(Markdig.Markdown.ToPlainText(text, GitHubLike)) })
        {
            var host = InstanceHostIn(view);
            if (host is not null)
            {
                return $"instance host {host}";
            }

            var email = EmailPattern().Match(view);
            if (email.Success)
            {
                return $"email {email.Value}";
            }

            foreach (var (name, pattern) in names)
            {
                if (pattern.IsMatch(view))
                {
                    return $"member name {name}";
                }
            }
        }

        return null;
    }

    // What a reader sees: entities and percent-escapes decoded, format characters (zero-width, soft hyphen) and emphasis
    // markers dropped, whitespace collapsed.
    private static string Normalise(string text)
    {
        var decoded = text;
        for (var pass = 0; pass < MaxDecodePasses; pass++)
        {
            var next = Uri.UnescapeDataString(WebUtility.HtmlDecode(decoded));
            if (string.Equals(next, decoded, StringComparison.Ordinal))
            {
                break;
            }

            decoded = next;
        }

        var kept = new StringBuilder(decoded.Length);
        foreach (var c in decoded)
        {
            if (c is not ('*' or '_' or '~') && char.GetUnicodeCategory(c) != UnicodeCategory.Format)
            {
                kept.Append(char.IsWhiteSpace(c) ? ' ' : c);
            }
        }

        return Whitespace().Replace(kept.ToString(), " ");
    }

    // A host-like run of letters, digits, hyphens and dots (any script's full stop), normalised the way a browser does it.
    private string? InstanceHostIn(string view)
    {
        foreach (var candidate in HostCandidate().Matches(view).Select(m => m.Value))
        {
            var host = AsciiOf(candidate);
            if (asciiHosts.Any(h => string.Equals(host, h, StringComparison.Ordinal) || host.EndsWith("." + h, StringComparison.Ordinal)))
            {
                return candidate;
            }

            if (addresses.Count > 0 && IPAddress.TryParse(candidate.Trim('[', ']'), out var ip) && addresses.Contains(Canonical(ip)))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string HostOf(string baseUrl)
    {
        var trimmed = baseUrl.Trim();
        return AsciiOf(Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.Host.Length > 0 ? uri.Host : trimmed.Trim('/'));
    }

    // Lower-cased punycode without a trailing dot; a host IdnMapping can't read is compared as written, lower-cased.
    private static string AsciiOf(string host)
    {
        var ascii = host;
        if (!Ascii.IsValid(host) || host.Contains("xn--", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                ascii = Idn.GetAscii(host.TrimEnd('.', '。', '．', '｡'));
            }
            catch (ArgumentException)
            {
                ascii = host;
            }
        }

        return ascii.ToLowerInvariant().TrimEnd('.');
    }

    private static string UnicodeOf(string asciiHost)
    {
        try
        {
            return Idn.GetUnicode(asciiHost);
        }
        catch (ArgumentException)
        {
            return asciiHost;
        }
    }

    private static IPAddress Canonical(IPAddress ip) => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

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

    // Decoded until it stops changing, so a double-encoded entity is decoded too; null if it never does.
    private static string? DecodeEntities(string text, List<string> removed)
    {
        var decoded = text;
        for (var pass = 0; pass < MaxDecodePasses; pass++)
        {
            var next = WebUtility.HtmlDecode(decoded);
            if (string.Equals(next, decoded, StringComparison.Ordinal))
            {
                if (!string.Equals(decoded, text, StringComparison.Ordinal))
                {
                    removed.Add("entities: decoded HTML entities");
                }

                return decoded;
            }

            decoded = next;
        }

        return null;
    }

    // Raw HTML (blocks, tags and comments) and links to the instance, repeated until a parse finds none: removing one tag can
    // join the text around it into another (<<b>script>). Null if it is still finding some after the last pass.
    private string? RemoveMarkup(string text, List<string> removed)
    {
        for (var pass = 0; pass < MaxMarkupPasses; pass++)
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

        return null;
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

    // The instance host or a subdomain of it, after percent-decoding; a URL still encoded after the last pass counts as one.
    private bool PointsToInstance(string? url)
    {
        if (string.IsNullOrEmpty(url) || hosts.Count == 0)
        {
            return false;
        }

        var decoded = url;
        for (var pass = 0; pass < MaxDecodePasses; pass++)
        {
            if (hosts.Any(h => h.IsMatch(decoded)))
            {
                return true;
            }

            var next = Uri.UnescapeDataString(decoded);
            if (string.Equals(next, decoded, StringComparison.Ordinal))
            {
                return false;
            }

            decoded = next;
        }

        return true;
    }

    // Mentions outside code spans and code blocks, other than the issue author's (a team mention is never theirs). The first
    // call wraps them in backticks; a stray backtick in the text can pair with one of those and leave a mention outside code,
    // so the second call breaks whatever is still live with a zero-width space after the @.
    private static string NeutraliseMentions(string text, string issueAuthor, Func<Match, string> neutralise, List<string> removed)
    {
        var edits = LiveMentions(text, issueAuthor).Select(m => new Edit(m.Index, m.Index + m.Length, neutralise(m), "mention")).ToList();
        return Apply(text, edits, removed);
    }

    private static List<Match> LiveMentions(string text, string issueAuthor)
    {
        var code = Markdig.Markdown.Parse(text, GitHubLike).Descendants()
            .Where(n => n is CodeInline or CodeBlock)
            .Select(n => (Start: n.Span.Start, End: n.Span.End + 1))
            .ToList();
        return MentionPattern().Matches(text)
            .Where(m => !code.Any(c => m.Index < c.End && c.Start < m.Index + m.Length))
            .Where(m => !string.Equals(m.Value.TrimStart('\\'), "@" + issueAuthor, StringComparison.OrdinalIgnoreCase))
            .ToList();
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

    // Letters, digits, marks, hyphens and full stops (ASCII, ideographic, full-width, half-width), or an IPv6 literal.
    [GeneratedRegex(@"\[?[0-9A-Fa-f.]*:[0-9A-Fa-f.]*:[0-9A-Fa-f:.]*\]?|[\p{L}\p{N}\p{M}\-.。．｡]+",RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex HostCandidate();

    [GeneratedRegex(" {2,}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 2000)]
    private static partial Regex Whitespace();

    private sealed record Edit(int Start, int End, string Replacement, string Kind);
}
