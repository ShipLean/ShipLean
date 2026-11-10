using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Asks;

public sealed record AskResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("number")] int Number,
    [property: JsonPropertyName("label")] string Label) : McpResult(Project, OpenUrl);
