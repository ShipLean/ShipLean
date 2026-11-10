using ShipLean.Contracts.Screens;

namespace ShipLean.Application.Features.FeaturesOverview;

// What changed on a project's Features since the current user last marked the digest seen, a page at a time (cursor from the
// previous page, null for the first). MarkSeenAsync records now as their seen time for the project. Callers check Member access first.
public interface IFeatureDigest
{
    Task<FeatureDigestDto> GetAsync(Guid projectId, string? cursor, CancellationToken cancellationToken);

    Task MarkSeenAsync(Guid projectId, CancellationToken cancellationToken);
}
