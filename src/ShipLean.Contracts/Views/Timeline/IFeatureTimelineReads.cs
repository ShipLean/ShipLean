namespace ShipLean.Contracts.Views.Timeline;

// A Feature's timeline read on the server with no viewer (the sprint demo data, SHIP-2330): no access check, as of now, labels in UTC.
// The caller decides who may see the result; featureId must be a Feature of projectId. A viewer's read goes through IFeatureTimeline.
public interface IFeatureTimelineReads
{
    Task<FeatureTimelineViewDto> ReadUtcAsync(Guid projectId, Guid featureId, CancellationToken cancellationToken);
}
