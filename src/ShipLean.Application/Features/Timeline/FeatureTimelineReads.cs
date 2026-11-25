using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Timeline;

// The timeline with no viewer (SHIP-2330): the same facts and pure builder as FeatureTimeline, as of now, in UTC, and no access check.
public sealed class FeatureTimelineReads(ITimelineFactsReader reader, TimeProvider clock) : IFeatureTimelineReads
{
    public async Task<FeatureTimelineViewDto> ReadUtcAsync(Guid projectId, Guid featureId, CancellationToken cancellationToken)
    {
        var asOf = clock.GetUtcNow();
        var facts = await reader.ReadAsync(projectId, featureId, asOf, cancellationToken);
        return TimelineBuilder.Build(facts, asOf, TimeZoneInfo.Utc);
    }
}
