using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// applied is true: the feature (its key), its stories and their tasks exist. Attachments are the file names attached to the feature.
public sealed record CapturePlanResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("applied")] bool Applied,
    [property: JsonPropertyName("feature")] string Feature,
    [property: JsonPropertyName("stories")] IReadOnlyList<CapturedStory> Stories,
    [property: JsonPropertyName("attachments")] IReadOnlyList<string> Attachments) : McpResult(Project, OpenUrl);
