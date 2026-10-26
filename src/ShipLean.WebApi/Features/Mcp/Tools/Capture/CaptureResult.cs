using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

public sealed record CaptureResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("items")] IReadOnlyList<CreatedItem> Items) : McpResult(Project, OpenUrl);
