using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.WebApi.Tests.Mcp.Timeline;

// A double for IFeatureTimeline that returns the pinned running fixture whatever the key: it has not-started items, with and
// without an assignee.
public sealed class RunningFixtureFeatureTimeline : IFeatureTimeline
{
    public static FeatureTimelineViewDto View { get; } = FixtureFeatureTimeline.Read("feature-timeline-running-view.json");

    public Task<FeatureTimelineViewDto> GetAsync(Guid projectId, string featureKey, CancellationToken ct) => Task.FromResult(View);
}
