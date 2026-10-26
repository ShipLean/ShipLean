using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

public sealed record CommentResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("comment_id")] Guid CommentId,
    [property: JsonPropertyName("agent_name")] string? AgentName) : McpResult(Project, OpenUrl);
