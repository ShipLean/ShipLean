using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// The prompt after the move: its status and the custom version now in use (null for the built-in).
internal sealed record UsePromptVersionResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("in_use_version")] int? InUseVersion) : McpResult(Project, OpenUrl);
