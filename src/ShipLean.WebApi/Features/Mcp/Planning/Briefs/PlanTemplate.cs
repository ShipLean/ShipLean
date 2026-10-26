using System.Text.RegularExpressions;

namespace ShipLean.WebApi.Features.Mcp.Planning.Briefs;

// Fills a guide template in one pass: each {{name}} in `values` is replaced by its value, and text that was put in is never
// scanned again, so a {{...}} inside a common brief or a task brief stays as written.
public static partial class PlanTemplate
{
    [GeneratedRegex(@"\{\{[a-z_]+\}\}")]
    private static partial Regex Placeholder();

    public static string Fill(string template, IReadOnlyDictionary<string, string> values)
        => Placeholder().Replace(template, match => values.TryGetValue(match.Value, out var value) ? value : match.Value);
}
