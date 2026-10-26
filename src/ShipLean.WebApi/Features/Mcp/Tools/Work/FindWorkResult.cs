using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Work;

internal sealed record FindWorkResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("sprint")] string Sprint,
    [property: JsonPropertyName("items")] IReadOnlyList<ReadyItem> Items) : McpResult(Project, OpenUrl);
