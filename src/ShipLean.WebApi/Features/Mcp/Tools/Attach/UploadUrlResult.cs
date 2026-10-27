using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Attach;

// attach_file without base64: nothing is attached yet. The agent runs `command` (curl -T) with the file's path in place of <file>.
public sealed record UploadUrlResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("upload_url")] string UploadUrl,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("command")] string Command) : McpResult(Project, OpenUrl);
