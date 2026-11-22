using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// The project's newest analyses (get_retro_analysis with no `id`).
internal sealed record ListRetroAnalysesResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("analyses")] IReadOnlyList<RetroAnalysisRow> Analyses) : McpResult(Project, OpenUrl);
