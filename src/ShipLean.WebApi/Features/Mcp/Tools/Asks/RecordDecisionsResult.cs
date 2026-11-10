using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Asks;

public sealed record RecordDecisionsResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("created")] IReadOnlyList<int> Created,
    [property: JsonPropertyName("updated")] IReadOnlyList<int> Updated) : McpResult(Project, OpenUrl);
