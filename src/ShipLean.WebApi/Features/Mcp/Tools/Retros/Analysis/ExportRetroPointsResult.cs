using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

internal sealed record ExportRetroPointsResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("scope")] RetroScopeView Scope,
    [property: JsonPropertyName("total_points")] int TotalPoints,
    [property: JsonPropertyName("next_cursor")] string? NextCursor,
    [property: JsonPropertyName("points")] IReadOnlyList<RetroExportPoint> Points,
    [property: JsonPropertyName("retros")] IReadOnlyDictionary<Guid, RetroExportRetro> Retros,
    [property: JsonPropertyName("open_actions")] IReadOnlyList<RetroExportOpenAction> OpenActions,
    [property: JsonPropertyName("previous_patterns")] IReadOnlyList<RetroExportPattern> PreviousPatterns) : McpResult(Project, OpenUrl);
