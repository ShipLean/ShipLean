using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// The arguments of record_retro_analysis as the plan digest sees them (never the plan token, the project or the payload id), in a fixed property order.
internal sealed record RecordRetroAnalysisInput
{
    [JsonPropertyName("feature")]
    public string? Feature { get; init; }

    [JsonPropertyName("sprint")]
    public string? Sprint { get; init; }

    [JsonPropertyName("from")]
    public string? From { get; init; }

    [JsonPropertyName("to")]
    public string? To { get; init; }

    [JsonPropertyName("exclude_covered")]
    public bool? ExcludeCovered { get; init; }

    [JsonPropertyName("embedding_model")]
    public string? EmbeddingModel { get; init; }

    [JsonPropertyName("rewrite_model")]
    public string? RewriteModel { get; init; }

    [JsonPropertyName("points")]
    public IReadOnlyList<RetroAnalysisPointInput>? Points { get; init; }

    [JsonPropertyName("patterns")]
    public IReadOnlyList<RetroAnalysisPatternInput>? Patterns { get; init; }

    [JsonPropertyName("fixes")]
    public IReadOnlyList<RetroAnalysisFixInput>? Fixes { get; init; }

    [JsonPropertyName("report_markdown")]
    public string? ReportMarkdown { get; init; }
}
