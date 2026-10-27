using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

public sealed record ReadAttachmentResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("content_type")] string ContentType,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("is_markdown")] bool IsMarkdown,
    [property: JsonPropertyName("markdown")] string? Markdown,
    [property: JsonPropertyName("too_large")] bool TooLarge,
    [property: JsonPropertyName("download_url")] string? DownloadUrl = null,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt = null,
    [property: JsonPropertyName("command")] string? Command = null) : McpResult(Project, OpenUrl);
