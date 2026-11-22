using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// applied false: a preview, nothing written (send the same arguments with `plan` to apply). AnalysisId and Created are set on an apply only.
internal sealed record RecordRetroAnalysisResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("funnel")] RetroFunnel Funnel,
    [property: JsonPropertyName("patterns")] IReadOnlyList<RetroRankedPattern> Patterns,
    [property: JsonPropertyName("would_create")] IReadOnlyList<DecideWouldCreateLine> WouldCreate,
    [property: JsonPropertyName("would_add")] IReadOnlyList<DecideWouldAddLine> WouldAdd,
    [property: JsonPropertyName("skipped")] IReadOnlyList<DecideSkippedLine> Skipped,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("analysis_id")] Guid? AnalysisId,
    [property: JsonPropertyName("created")] IReadOnlyList<string> Created,
    [property: JsonPropertyName("plan")] string? Plan,
    [property: JsonPropertyName("expires_in_minutes")] int? ExpiresInMinutes) : McpResult(Project, OpenUrl);
