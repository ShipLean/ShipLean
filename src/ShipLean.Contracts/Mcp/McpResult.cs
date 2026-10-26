using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Mcp;

// What every successful tool result carries: the project key it concerns (null for an install-level tool) and an
// absolute URL (McpOpenUrls) the person can open to see it. A tool's own result derives from this record and adds its
// fields, snake_case on the wire like these two.
// Rule: every property of a type deriving McpResult carries [JsonPropertyName] with a snake_case name (the web
// serializer options would otherwise write multi-word names camelCase); McpResultWireNameTests enforces it. Same CallToolResult convention as McpError: StructuredContent is the
// JSON, Content is one line of text, IsError is false.
public record McpResult(
    [property: JsonPropertyName("project")] string? Project,
    [property: JsonPropertyName("open_url")] string OpenUrl);
