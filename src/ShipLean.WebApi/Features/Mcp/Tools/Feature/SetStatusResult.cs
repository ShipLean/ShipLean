using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Feature;

public sealed record SetStatusResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("verified")] VerifiedLine? Verified,
    [property: JsonPropertyName("revision")] int Revision) : McpResult(Project, OpenUrl);
