using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Prompts;

// prompts with `prompt`: one prompt, the text of one version (TextOf: "v3" or "built-in") and its versions, newest first.
internal sealed record PromptDetailResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("when_runs")] string WhenRuns,
    [property: JsonPropertyName("served_by")] string ServedBy,
    [property: JsonPropertyName("read_by")] string ReadBy,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("in_use_version")] int? InUseVersion,
    [property: JsonPropertyName("placeholders")] IReadOnlyList<string> Placeholders,
    [property: JsonPropertyName("inserted_parts")] IReadOnlyList<string> InsertedParts,
    [property: JsonPropertyName("text_of")] string TextOf,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("versions")] IReadOnlyList<PromptVersionLine> Versions,
    [property: JsonPropertyName("can_edit")] bool CanEdit) : McpResult(Project, OpenUrl);
