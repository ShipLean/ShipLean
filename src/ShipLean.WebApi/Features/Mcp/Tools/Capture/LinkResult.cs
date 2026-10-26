using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

public sealed record LinkResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("linked_to")] string LinkedTo,
    [property: JsonPropertyName("link_type")] string LinkType,
    [property: JsonPropertyName("revision")] int Revision) : McpResult(Project, OpenUrl);
