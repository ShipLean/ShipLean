namespace ShipLean.Contracts.Views.Timeline;

// A Feature's timeline (C2): the one read behind GET …/by-key/{itemKey}/timeline and the MCP tool feature_timeline. The caller
// must be a member of the project; a key that isn't a Feature in that project is not_found.
public interface IFeatureTimeline
{
    Task<FeatureTimelineViewDto> GetAsync(Guid projectId, string featureKey, CancellationToken ct);
}
