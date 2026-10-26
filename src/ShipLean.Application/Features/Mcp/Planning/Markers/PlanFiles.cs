using System.Globalization;
using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Mcp.Planning.Markers;

// The names of the files a plan attaches to its Feature.
public static partial class PlanFiles
{
    public const string CommonBrief = "common-brief.md";
    public const string Plan = "PLAN.md";

    // retro-<yyyy-mm-dd>-<topic>.md, with a real date and a non-empty topic of any characters but / and \. The retro- prefix
    // and the .md extension match in any case.
    public static bool IsRetro(string name)
    {
        var match = RetroName().Match(name);
        return match.Success
               && DateOnly.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }

    [GeneratedRegex(@"^retro-([0-9]{4}-[0-9]{2}-[0-9]{2})-[^/\\]+\.md\z", RegexOptions.IgnoreCase)]
    private static partial Regex RetroName();
}
