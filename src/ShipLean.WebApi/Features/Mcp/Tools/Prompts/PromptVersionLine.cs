using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// One saved version of a prompt, newest first. Agent is set when an agent saved it over MCP; source is editor, builtInUpdate, migrated or mcp.
public sealed record PromptVersionLine(
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("note")] string Note,
    [property: JsonPropertyName("by")] string By,
    [property: JsonPropertyName("agent")] string? Agent,
    [property: JsonPropertyName("at")] string At,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("in_use")] bool InUse);
