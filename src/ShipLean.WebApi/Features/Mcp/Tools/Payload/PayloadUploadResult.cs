using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Payload;

// create_payload_upload: nothing is stored yet. The agent runs `command` (curl -T) with the file's path in place of <file>,
// then calls `tool` with payload set to `payload`. Field null: the file is a JSON object of the tool's arguments.
public sealed record PayloadUploadResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("field")] string? Field,
    [property: JsonPropertyName("payload")] string Payload,
    [property: JsonPropertyName("upload_url")] string UploadUrl,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("command")] string Command) : McpResult(Project, OpenUrl);
