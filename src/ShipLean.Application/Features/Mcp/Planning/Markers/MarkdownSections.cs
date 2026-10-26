using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Mcp.Planning.Markers;

// One section of a Markdown document by its heading text.
public static partial class MarkdownSections
{
    // The text under the first ATX heading (any level) whose text is `heading`, ignoring case and a closing run of #, up to
    // the next heading of the same level or higher, trimmed. Empty when the heading has nothing under it; null when there is
    // no such heading. Lines inside fenced code blocks are never headings.
    public static string? Extract(string? markdown, string heading)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return null;
        }

        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        int? level = null;
        var start = 0;
        string? fence = null;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (Fence().Match(line) is { Success: true } opening)
            {
                var marker = opening.Groups[1].Value;
                if (fence is null)
                {
                    fence = marker;
                }
                else if (marker[0] == fence[0] && marker.Length >= fence.Length && line.Trim().Length == marker.Length)
                {
                    fence = null;
                }
                continue;
            }
            if (fence is not null || Heading().Match(line) is not { Success: true } match)
            {
                continue;
            }

            var depth = match.Groups[1].Length;
            if (level is null)
            {
                if (string.Equals(match.Groups[2].Value.Trim(), heading.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    level = depth;
                    start = i + 1;
                }
            }
            else if (depth <= level)
            {
                return Join(lines, start, i);
            }
        }
        return level is null ? null : Join(lines, start, lines.Length);
    }

    private static string Join(string[] lines, int start, int end) => string.Join('\n', lines[start..end]).Trim();

    [GeneratedRegex(@"^ {0,3}(#{1,6})(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*\z")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})")]
    private static partial Regex Fence();
}
