using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Mcp;

// The "error" object of an McpError.
public sealed record McpErrorBody(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("field")] string? Field,
    [property: JsonPropertyName("candidates")] IReadOnlyList<string>? Candidates);
