using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

public sealed record RetroAnalysisFixInput
{
    [JsonPropertyName("pattern_ref"), Description("The ref of the problem pattern this fix is for.")]
    public required string PatternRef { get; init; }

    [JsonPropertyName("title"), Description("The fix, as an action item title.")]
    public required string Title { get; init; }

    [JsonPropertyName("fix_type"), Description("fix, structure, analyzer, test, skill, guide, product or other.")]
    public required string FixType { get; init; }

    [JsonPropertyName("enforcement"), Description("notCaught, partly, fully or discarded: how much what already exists does it. Fully and discarded never create or extend an action item.")]
    public required string Enforcement { get; init; }

    [JsonPropertyName("enforced_by"), Description("What already does it. Needed for partly and fully.")]
    public string? EnforcedBy { get; init; }

    [JsonPropertyName("reason"), Description("Why the fix, or why it was discarded.")]
    public required string Reason { get; init; }

    [JsonPropertyName("existing"), Description("The key of an open action item to add the pattern's points to instead of creating one.")]
    public string? Existing { get; init; }
}
