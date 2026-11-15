namespace ShipLean.Application.Features.Inspection.Views;

// What a project that hides content (D4) never sends: prompt text, tool input and output, and the attribute keys that carry them. The
// assembler leaves these in RunRow.Attributes and in each row log's attributes, so every view strips them there too.
public static class ContentKeys
{
    public static readonly IReadOnlySet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "prompt", "prompt_text", "input", "tool_input", "tool_parameters", "tool_output", "output", "response", "result", "content", "command",
    };

    public static IEnumerable<KeyValuePair<string, string>> Visible(IReadOnlyDictionary<string, string> attributes, bool contentShown)
        => contentShown ? attributes : attributes.Where(a => !Hidden.Contains(a.Key));
}
