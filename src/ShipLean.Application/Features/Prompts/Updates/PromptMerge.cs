using System.Text;

namespace ShipLean.Application.Features.Prompts.Updates;

public static class PromptMerge
{
    private const string MineMarker = "<<<<<<< yours\n";
    private const string Divider = "=======\n";
    private const string TheirsMarker = ">>>>>>> built-in\n";

    /// <summary>Three-way merge by lines. mine = the project's custom text, theirs = the new built-in, baseText = the built-in the custom started from.</summary>
    public static PromptMergeResult Merge(string baseText, string mine, string theirs)
    {
        string[] baseLines = [.. TextLines.Split(baseText)];
        var regions = Regions(baseLines, [.. TextLines.Split(mine)], mine: true)
            .Concat(Regions(baseLines, [.. TextLines.Split(theirs)], mine: false))
            .OrderBy(r => r.Start).ThenBy(r => r.End)
            .ToList();

        var output = new StringBuilder();
        var clean = true;
        var position = 0;
        var next = 0;
        while (next < regions.Count)
        {
            // Changes that overlap or touch (even only at one line boundary) cannot be told apart safely, so they form one group.
            var groupStart = regions[next].Start;
            var groupEnd = regions[next].End;
            var group = new List<MergeRegion> { regions[next] };
            next++;
            while (next < regions.Count && regions[next].Start <= groupEnd)
            {
                groupEnd = Math.Max(groupEnd, regions[next].End);
                group.Add(regions[next]);
                next++;
            }

            AppendLines(output, baseLines[position..groupStart]);
            position = groupEnd;

            var yours = Apply(baseLines, group.Where(r => r.Mine).ToList(), groupStart, groupEnd);
            var theirsText = Apply(baseLines, group.Where(r => !r.Mine).ToList(), groupStart, groupEnd);
            if (group.All(r => r.Mine) || group.All(r => !r.Mine) || yours.SequenceEqual(theirsText))
            {
                // One side only, or both made the very same change: the result is that side once.
                AppendLines(output, group[0].Mine ? yours : theirsText);
                continue;
            }

            clean = false;
            output.Append(MineMarker);
            AppendSegment(output, yours);
            output.Append(Divider);
            AppendSegment(output, theirsText);
            output.Append(TheirsMarker);
        }

        AppendLines(output, baseLines[position..]);
        return new PromptMergeResult(output.ToString(), clean);
    }

    private static List<MergeRegion> Regions(string[] baseLines, string[] side, bool mine)
    {
        var edits = LineDiff.Edits(baseLines, side);
        var regions = new List<MergeRegion>();
        var index = 0;
        while (index < edits.Count)
        {
            if (edits[index].Kind == ' ')
            {
                index++;
                continue;
            }

            var start = edits[index].A;
            var end = start;
            var lines = new List<string>();
            while (index < edits.Count && edits[index].Kind != ' ')
            {
                if (edits[index].Kind == '-')
                {
                    end = edits[index].A + 1;
                }
                else
                {
                    lines.Add(side[edits[index].B]);
                }

                index++;
            }

            regions.Add(new MergeRegion(start, end, [.. lines], mine));
        }

        return regions;
    }

    // The base lines [from, to) with this side's regions swapped in.
    private static string[] Apply(string[] baseLines, List<MergeRegion> regions, int from, int to)
    {
        var lines = new List<string>();
        var position = from;
        foreach (var region in regions)
        {
            lines.AddRange(baseLines[position..region.Start]);
            lines.AddRange(region.Lines);
            position = region.End;
        }

        lines.AddRange(baseLines[position..to]);
        return [.. lines];
    }

    private static void AppendLines(StringBuilder output, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            output.Append(line);
        }
    }

    // A marker must start on its own line, so a segment whose last line has no newline gets one here.
    private static void AppendSegment(StringBuilder output, string[] lines)
    {
        AppendLines(output, lines);
        if (lines.Length > 0 && !lines[^1].EndsWith('\n'))
        {
            output.Append('\n');
        }
    }
}
