using System.Text;
using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Demos.Data;

// Reads a Feature description's "## Why" section (K7). The section runs from the heading to the next "## " heading or the end;
// headings inside a code fence don't count, either as the start or as the end. The measure is the text after "**Measure" in that
// section: its line and the lines that continue it, up to a blank line or the next bullet. A "(to confirm)" marker beside the
// word Measure is kept, moved to the end of the text.
public static partial class DemoWhy
{
    private const string ToConfirm = "(to confirm)";

    public static DemoWhyParts Extract(string? description)
    {
        var section = Section(description ?? string.Empty);
        return section is null ? new DemoWhyParts(null, null) : new DemoWhyParts(section, Measure(section));
    }

    private static string? Section(string description)
    {
        var lines = description.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var inFence = false;
        var inWhy = false;
        var body = new StringBuilder();
        var found = false;
        foreach (var line in lines)
        {
            if (FenceLine().IsMatch(line))
            {
                inFence = !inFence;
            }
            else if (!inFence && SectionHeading().IsMatch(line))
            {
                if (inWhy)
                {
                    break;
                }
                inWhy = WhyHeading().IsMatch(line);
                found |= inWhy;
                continue;
            }
            if (inWhy)
            {
                body.Append(line).Append('\n');
            }
        }
        var text = body.ToString().Trim();
        return found && text.Length > 0 ? text : null;
    }

    private static string? Measure(string section)
    {
        var lines = section.Split('\n');
        var inFence = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (FenceLine().IsMatch(lines[i]))
            {
                inFence = !inFence;
                continue;
            }
            var match = inFence ? Match.Empty : MeasureStart().Match(lines[i]);
            if (!match.Success)
            {
                continue;
            }
            var text = new StringBuilder(match.Groups["text"].Value.Trim());
            for (var next = i + 1; next < lines.Length && IsContinuation(lines[next]); next++)
            {
                text.Append(' ').Append(lines[next].Trim());
            }
            var measure = text.ToString().Trim();
            if (match.Groups["confirm"].Success)
            {
                measure = $"{measure} {ToConfirm}".Trim();
            }
            return measure.Length > 0 ? measure : null;
        }
        return null;
    }

    private static bool IsContinuation(string line)
        => line.Trim().Length > 0 && !BulletOrBold().IsMatch(line) && !FenceLine().IsMatch(line);

    [GeneratedRegex(@"^\s{0,3}(```|~~~)")]
    private static partial Regex FenceLine();

    [GeneratedRegex(@"^## ")]
    private static partial Regex SectionHeading();

    [GeneratedRegex(@"^##\s+Why\s*#*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex WhyHeading();

    [GeneratedRegex(@"\*\*Measure(?<confirm>\s*\(to confirm\))?\s*(?::\s*\*\*|\*\*\s*:?)(?<text>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex MeasureStart();

    [GeneratedRegex(@"^\s*([-*+]\s|\*\*|\d+\.\s)")]
    private static partial Regex BulletOrBold();
}
