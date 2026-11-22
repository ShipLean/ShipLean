using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// One analysis (get_retro_analysis with `id`).
internal sealed record GetRetroAnalysisResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("scope")] RetroScopeView Scope,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("agent_name")] string? AgentName,
    [property: JsonPropertyName("embedding_model")] string EmbeddingModel,
    [property: JsonPropertyName("rewrite_model")] string? RewriteModel,
    [property: JsonPropertyName("funnel")] RetroFunnel Funnel,
    [property: JsonPropertyName("patterns")] IReadOnlyList<RetroViewPattern> Patterns,
    [property: JsonPropertyName("went_well")] IReadOnlyList<RetroViewPattern> WentWell,
    [property: JsonPropertyName("fixes_by_tier")] IReadOnlyDictionary<string, IReadOnlyList<RetroViewFix>> FixesByTier,
    [property: JsonPropertyName("discarded")] IReadOnlyList<RetroViewFix> Discarded,
    [property: JsonPropertyName("report_markdown")] string ReportMarkdown) : McpResult(Project, OpenUrl);
