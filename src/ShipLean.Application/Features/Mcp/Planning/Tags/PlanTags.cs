using System.Globalization;
using System.Text.RegularExpressions;
using ShipLean.Application.Features.Mcp.Planning.Waves;

namespace ShipLean.Application.Features.Mcp.Planning.Tags;

// The plan's tags on work items: wave:<w>, lane:<l>, model:<m>, ctx:<n>k, branch:<name> and orphan. The only place that
// writes or reads them; every other tag on the item is the person's and is left alone.
// Placement: a Feature carries branch:. A story carries wave: and lane: (when it has one). A task carries its story's wave:
// and lane:, plus its own model: and ctx:, and orphan when it is one.
public static partial class PlanTags
{
    public const string Wave = "wave:";
    public const string Lane = "lane:";
    public const string Model = "model:";
    public const string Context = "ctx:";
    public const string Branch = "branch:";
    public const string Orphan = "orphan";

    // The tags for what is set, in a fixed order: wave, lane, model, ctx, branch, orphan.
    public static IReadOnlyList<string> Format(PlanTaskTags tags)
    {
        var formatted = new List<string>();
        if (tags.Wave is not null)
        {
            formatted.Add(Wave + tags.Wave.Text);
        }
        if (!string.IsNullOrWhiteSpace(tags.Lane))
        {
            formatted.Add(Lane + tags.Lane);
        }
        if (!string.IsNullOrWhiteSpace(tags.Model))
        {
            formatted.Add(Model + tags.Model);
        }
        if (tags.ContextK is { } contextK)
        {
            formatted.Add(Context + contextK.ToString(CultureInfo.InvariantCulture) + "k");
        }
        if (!string.IsNullOrWhiteSpace(tags.Branch))
        {
            formatted.Add(Branch + tags.Branch);
        }
        if (tags.Orphan)
        {
            formatted.Add(Orphan);
        }
        return formatted;
    }

    // The first well-formed tag of each kind wins. A malformed one (wave:soon, ctx:lots) reads as absent.
    public static PlanTaskTags Parse(IEnumerable<string> tags)
    {
        var parsed = new PlanTaskTags();
        foreach (var tag in tags)
        {
            if (parsed.Wave is null && Value(tag, Wave) is { } wave && WaveLabel.TryParse(wave, out var label))
            {
                parsed = parsed with { Wave = label };
            }
            else if (parsed.Lane is null && Value(tag, Lane) is { } lane)
            {
                parsed = parsed with { Lane = lane };
            }
            else if (parsed.Model is null && Value(tag, Model) is { } model)
            {
                parsed = parsed with { Model = model };
            }
            else if (parsed.ContextK is null && ContextTag().Match(tag) is { Success: true } match)
            {
                parsed = parsed with { ContextK = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) };
            }
            else if (parsed.Branch is null && Value(tag, Branch) is { } branch)
            {
                parsed = parsed with { Branch = branch };
            }
            else if (tag == Orphan)
            {
                parsed = parsed with { Orphan = true };
            }
        }
        return parsed;
    }

    // True for a tag this class writes, so a writer can replace the plan's tags and keep the person's.
    public static bool IsPlanTag(string tag)
        => tag == Orphan || new[] { Wave, Lane, Model, Context, Branch }.Any(prefix => tag.StartsWith(prefix, StringComparison.Ordinal));

    private static string? Value(string tag, string prefix)
        => tag.StartsWith(prefix, StringComparison.Ordinal) && tag.Length > prefix.Length && !string.IsNullOrWhiteSpace(tag[prefix.Length..])
            ? tag[prefix.Length..]
            : null;

    [GeneratedRegex(@"^ctx:([0-9]{1,9})k\z")]
    private static partial Regex ContextTag();
}
