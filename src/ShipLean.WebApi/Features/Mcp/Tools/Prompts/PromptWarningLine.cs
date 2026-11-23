using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// A check that doesn't stop a save. Kind is placeholderMissing, placeholderUnknown or unknownTag.
public sealed record PromptWarningLine(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("placeholder")] string? Placeholder,
    [property: JsonPropertyName("tag")] string? Tag,
    [property: JsonPropertyName("line")] int? Line);
