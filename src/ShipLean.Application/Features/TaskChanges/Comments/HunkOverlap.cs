using System.Text.RegularExpressions;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Comments;

// Whether a unified git diff has a hunk over a comment's lines. A comment on the old side is matched against the file's old
// path and old line numbers, one on the new side against the new path and new numbers. A hunk covers the lines its header
// names on that side, context included; a side with no lines (a deleted file's new side) covers nothing.
public static partial class HunkOverlap
{
    public static bool Touches(string unifiedDiff, string path, CodeSide side, int line, int? startLine)
    {
        var low = Math.Min(line, startLine ?? line);
        var high = Math.Max(line, startLine ?? line);
        var onSide = false;
        foreach (var text in unifiedDiff.Split('\n'))
        {
            if (FileHeader().Match(text) is { Success: true } file)
            {
                onSide = string.Equals(file.Groups[side == CodeSide.Old ? 1 : 2].Value, path, StringComparison.Ordinal);
            }
            else if (onSide && HunkHeader().Match(text) is { Success: true } hunk)
            {
                var start = int.Parse(hunk.Groups[side == CodeSide.Old ? 1 : 3].Value, System.Globalization.CultureInfo.InvariantCulture);
                var countGroup = hunk.Groups[side == CodeSide.Old ? 2 : 4];
                var count = countGroup.Success ? int.Parse(countGroup.Value, System.Globalization.CultureInfo.InvariantCulture) : 1;
                if (count > 0 && start <= high && low <= start + count - 1)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [GeneratedRegex("^diff --git a/(.+) b/(.+)$")]
    private static partial Regex FileHeader();

    [GeneratedRegex(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@")]
    private static partial Regex HunkHeader();
}
