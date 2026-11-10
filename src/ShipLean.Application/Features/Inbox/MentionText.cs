namespace ShipLean.Application.Features.Inbox;

// Finds the members a comment body @mentions by full name: case-insensitive, longest name first, at the start of a line or after
// whitespace, ending at the end, whitespace or .,;:!?) and never inside code. excludeUserId (the author) is never returned.
// Each person counts once, in the order first named, and at most MaxMentions are returned.
public static class MentionText
{
    public const int MaxMentions = 20;

    private const string NameEnds = ".,;:!?)";

    // Stands in for every character of a code span or fenced block: not whitespace, so code next to an `@` stops a mention.
    private const char Blanked = '\u0001';

    public static IReadOnlyList<Guid> Find(string body, IReadOnlyList<MentionCandidate> members, Guid? excludeUserId)
    {
        var text = WithoutCode(body);
        var longestFirst = members.Where(m => !string.IsNullOrWhiteSpace(m.Name)).OrderByDescending(m => m.Name.Length).ToList();
        var found = new List<Guid>();
        for (var at = text.IndexOf('@'); at >= 0 && found.Count < MaxMentions; at = text.IndexOf('@', at + 1))
        {
            if (at > 0 && !char.IsWhiteSpace(text[at - 1]))
            {
                continue;
            }

            var named = longestFirst.FirstOrDefault(m => IsNamedAt(text, at + 1, m.Name));
            if (named is not null && named.UserId != excludeUserId && !found.Contains(named.UserId))
            {
                found.Add(named.UserId);
            }
        }

        return found;
    }

    private static bool IsNamedAt(string text, int start, string name)
    {
        var end = start + name.Length;
        if (end > text.Length || string.Compare(text, start, name, 0, name.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return false;
        }

        return end == text.Length || char.IsWhiteSpace(text[end]) || NameEnds.Contains(text[end]);
    }

    // The body with fenced code blocks and inline code spans blanked out, keeping every line break and every other position.
    private static string WithoutCode(string body)
    {
        var chars = body.ToCharArray();
        BlankFencedBlocks(chars);
        BlankCodeSpans(chars);
        return new string(chars);
    }

    // A fence is a line of three or more ` or ~ indented at most three spaces; it closes at a line of at least as many of the same
    // character and nothing else, or runs to the end of the body.
    private static void BlankFencedBlocks(char[] chars)
    {
        var fence = '\0';
        var fenceLength = 0;
        for (var lineStart = 0; lineStart < chars.Length;)
        {
            var lineEnd = Array.IndexOf(chars, '\n', lineStart);
            if (lineEnd < 0)
            {
                lineEnd = chars.Length;
            }

            var (marker, run, rest) = FenceRun(chars, lineStart, lineEnd);
            if (fence == '\0' && run >= 3)
            {
                fence = marker;
                fenceLength = run;
                Blank(chars, lineStart, lineEnd);
            }
            else if (fence != '\0')
            {
                if (marker == fence && run >= fenceLength && rest)
                {
                    fence = '\0';
                }

                Blank(chars, lineStart, lineEnd);
            }

            lineStart = lineEnd + 1;
        }
    }

    // The fence character a line starts with, how many of it in a row, and whether only whitespace follows them.
    private static (char Marker, int Run, bool OnlyWhitespaceAfter) FenceRun(char[] chars, int lineStart, int lineEnd)
    {
        var i = lineStart;
        while (i < lineEnd && i - lineStart < 3 && chars[i] == ' ')
        {
            i++;
        }

        if (i >= lineEnd || (chars[i] != '`' && chars[i] != '~'))
        {
            return ('\0', 0, false);
        }

        var marker = chars[i];
        var runStart = i;
        while (i < lineEnd && chars[i] == marker)
        {
            i++;
        }

        var run = i - runStart;
        var onlyWhitespaceAfter = true;
        for (; i < lineEnd; i++)
        {
            onlyWhitespaceAfter &= char.IsWhiteSpace(chars[i]);
        }

        return (marker, run, onlyWhitespaceAfter);
    }

    // A code span opens with a run of backticks and closes at the next run of exactly as many; an unclosed run is plain text.
    private static void BlankCodeSpans(char[] chars)
    {
        var i = 0;
        while (i < chars.Length)
        {
            if (chars[i] != '`')
            {
                i++;
                continue;
            }

            var run = BacktickRun(chars, i);
            var close = FindClosingRun(chars, i + run, run);
            if (close < 0)
            {
                i += run;
                continue;
            }

            Blank(chars, i, close + run);
            i = close + run;
        }
    }

    private static int FindClosingRun(char[] chars, int from, int length)
    {
        var i = from;
        while (i < chars.Length)
        {
            if (chars[i] != '`')
            {
                i++;
                continue;
            }

            var run = BacktickRun(chars, i);
            if (run == length)
            {
                return i;
            }

            i += run;
        }

        return -1;
    }

    private static int BacktickRun(char[] chars, int start)
    {
        var end = start;
        while (end < chars.Length && chars[end] == '`')
        {
            end++;
        }

        return end - start;
    }

    private static void Blank(char[] chars, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (chars[i] != '\n')
            {
                chars[i] = Blanked;
            }
        }
    }
}
