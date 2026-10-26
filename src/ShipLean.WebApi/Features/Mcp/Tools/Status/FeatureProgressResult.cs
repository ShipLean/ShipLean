using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Status;

// ByType is the work tree roll-up under the item, one entry per child type that has any, at any depth. PointsDone and
// PointsTotal add the Story and Bug entries (null when none is estimated). OpenChildren lists up to 100 of the OpenTotal
// open descendants, with the sprint each is in.
internal sealed record FeatureProgressResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("item")] string Item,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("by_type")] IReadOnlyList<FeatureProgressResult.TypeProgress> ByType,
    [property: JsonPropertyName("points_done")] decimal? PointsDone,
    [property: JsonPropertyName("points_total")] decimal? PointsTotal,
    [property: JsonPropertyName("open_total")] int OpenTotal,
    [property: JsonPropertyName("open_children")] IReadOnlyList<FeatureProgressResult.OpenChild> OpenChildren) : McpResult(Project, OpenUrl)
{
    internal sealed record TypeProgress(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("open")] int Open,
        [property: JsonPropertyName("closed")] int Closed,
        [property: JsonPropertyName("points_done")] decimal? PointsDone,
        [property: JsonPropertyName("points_total")] decimal? PointsTotal,
        [property: JsonPropertyName("unestimated")] int Unestimated);

    internal sealed record OpenChild(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("sprint")] string? Sprint,
        [property: JsonPropertyName("assignee")] string? Assignee,
        [property: JsonPropertyName("points")] decimal? Points);
}
