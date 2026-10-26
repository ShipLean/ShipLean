using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

internal sealed record MovedItem(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("changed")] bool Changed,
    [property: JsonPropertyName("revision")] int Revision);
