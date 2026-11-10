using ShipLean.Contracts.Screens;

namespace ShipLean.Application.Features.FeaturesOverview;

public sealed class FeatureDigest(TimeProvider clock) : IFeatureDigest
{
    // TODO: read the caller's feature_digest_seen row and list the asks waiting on them (AskOwnerSql) and the Features' changes since
    // then (IWorkItemChanges), newest first, a page per cursor. Until then: never seen, nothing listed, no next page.
    public Task<FeatureDigestDto> GetAsync(Guid projectId, string? cursor, CancellationToken cancellationToken)
    {
        var (since, sinceLabel) = FeaturesSince.Of(null, clock);
        return Task.FromResult(new FeatureDigestDto(since, sinceLabel, [], [], null, false));
    }

    // TODO: upsert the caller's feature_digest_seen row for the project at the clock's now. Until then nothing is stored.
    public Task MarkSeenAsync(Guid projectId, CancellationToken cancellationToken) => Task.CompletedTask;
}
