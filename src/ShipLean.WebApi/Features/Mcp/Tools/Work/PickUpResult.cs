using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Work;

internal sealed record PickUpResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("assigned_to_you")] bool AssignedToYou,
    [property: JsonPropertyName("revision")] int Revision,
    [property: JsonPropertyName("notices")] IReadOnlyList<string> Notices) : McpResult(Project, OpenUrl);
