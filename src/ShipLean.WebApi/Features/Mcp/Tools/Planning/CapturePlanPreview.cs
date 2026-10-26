using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// applied is false: nothing was created. Feature is the existing feature's key, or the new one's title. Items counts every
// work item applying would create. Call capture_plan again with the same arguments plus `plan` to create exactly this.
public sealed record CapturePlanPreview(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("feature")] string Feature,
    [property: JsonPropertyName("stories")] IReadOnlyList<PlanPreviewStory> Stories,
    [property: JsonPropertyName("items")] int Items,
    [property: JsonPropertyName("warnings")] IReadOnlyList<PlanWarning> Warnings,
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("expires_in_minutes")] int ExpiresInMinutes) : McpResult(Project, OpenUrl);
