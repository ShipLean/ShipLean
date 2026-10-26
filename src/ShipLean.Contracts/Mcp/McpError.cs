using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Mcp;

// What a tool returns when it fails: { "ok": false, "error": { "code", "message", "field", "candidates" } }.
// Code is one of the ApiErrorCodes values (the snake_case set); Field names the argument at fault, Candidates
// the names to choose from when the code is "ambiguous"; both null otherwise.
// Wire convention for every tool result: the tool returns this (or its McpResult) as CallToolResult.StructuredContent,
// with a one-line text summary in Content, and sets IsError = true for an McpError. Property names are fixed here with
// JsonPropertyName so they do not depend on the serializer's naming policy. A missing scope is not an McpError: it is
// an HTTP 403 step-up.
public sealed record McpError(
    [property: JsonPropertyName("error")] McpErrorBody Error)
{
    // Always false: an McpError is never a success, so it cannot be set.
    [JsonPropertyName("ok")]
    public bool Ok => false;

    public static McpError Create(string code, string message, string? field = null, IReadOnlyList<string>? candidates = null)
        => new(new McpErrorBody(code, message, field, candidates));
}
