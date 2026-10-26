using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

// applied is false: nothing was created. Call capture_work again with `plan` to create exactly these items.
public sealed record CapturePreview(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("items")] IReadOnlyList<PreviewItem> Items,
    [property: JsonPropertyName("plan")] string Plan,
    [property: JsonPropertyName("expires_in_minutes")] int ExpiresInMinutes) : McpResult(Project, OpenUrl);
