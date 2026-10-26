using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

// A reorder writes no revision: Revision is the item's, unchanged.
internal sealed record ReorderResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("item")] string Item,
    [property: JsonPropertyName("position")] string Position,
    [property: JsonPropertyName("revision")] int Revision) : McpResult(Project, OpenUrl);
