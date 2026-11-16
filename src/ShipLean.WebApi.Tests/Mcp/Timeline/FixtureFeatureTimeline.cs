using System.Text.Json;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Mcp.Timeline;

// A double for IFeatureTimeline that returns the pinned HTTP fixture whatever the key, so the tool test checks the MCP naming
// and not the builder.
public sealed class FixtureFeatureTimeline : IFeatureTimeline
{
    public static FeatureTimelineViewDto View { get; } = Read();

    public Task<FeatureTimelineViewDto> GetAsync(Guid projectId, string featureKey, CancellationToken ct) => Task.FromResult(View);

    private static FeatureTimelineViewDto Read()
        => JsonSerializer.Deserialize<FeatureTimelineViewDto>(
            File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "DtoFixtures", "views", "feature-timeline-view.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })!;
}
