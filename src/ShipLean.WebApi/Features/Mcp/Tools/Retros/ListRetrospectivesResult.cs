using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

internal sealed record ListRetrospectivesResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("points")] IReadOnlyList<RetroPointLine> Points,
    [property: JsonPropertyName("theme_mode")] string ThemeMode,
    [property: JsonPropertyName("themes")] IReadOnlyList<RetroThemeLine> Themes) : McpResult(Project, OpenUrl);
