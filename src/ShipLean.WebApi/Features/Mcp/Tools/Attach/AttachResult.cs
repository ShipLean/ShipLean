using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Attach;

// replaced: a live file of the same name was replaced (its earlier version stays in the item's history).
public sealed record AttachResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("attachment_id")] Guid AttachmentId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("replaced")] bool Replaced,
    [property: JsonPropertyName("via")] string? Via) : McpResult(Project, OpenUrl);
