using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

// Items are the top of the level's stack in stack order, each with its flags. Counts tally the flags over those items.
internal sealed record ReviewBacklogResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("stale_days")] int StaleDays,
    [property: JsonPropertyName("examined")] int Examined,
    [property: JsonPropertyName("counts")] FlagCounts Counts,
    [property: JsonPropertyName("items")] IReadOnlyList<ReviewedItem> Items) : McpResult(Project, OpenUrl);
