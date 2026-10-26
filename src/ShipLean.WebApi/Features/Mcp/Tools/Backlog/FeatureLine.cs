using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

// Stories and bugs under the feature, from the work tree roll-up (everything under it, not only this sprint's).
internal sealed record FeatureLine(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("open")] int Open,
    [property: JsonPropertyName("closed")] int Closed,
    [property: JsonPropertyName("points_done")] decimal? PointsDone,
    [property: JsonPropertyName("points_total")] decimal? PointsTotal);
