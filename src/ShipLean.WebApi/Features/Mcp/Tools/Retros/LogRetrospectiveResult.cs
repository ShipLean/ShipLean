using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// Role is conductor or subAgent. ThemeMode is auto or pick: in pick mode the agent names the theme itself.
internal sealed record LogRetrospectiveResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("retro_id")] Guid RetroId,
    [property: JsonPropertyName("item")] string Item,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("points_added")] int PointsAdded,
    [property: JsonPropertyName("theme_mode")] string ThemeMode,
    [property: JsonPropertyName("themes")] IReadOnlyList<RetroThemeLine> Themes,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings) : McpResult(Project, OpenUrl);
