using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

public sealed record OrphanTask(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title);
