using ShipLean.Contracts.Screens;

namespace ShipLean.Application.Features.FeaturesOverview;

public sealed class FeaturesOverview(TimeProvider clock) : IFeaturesOverview
{
    // TODO: read the caller's feature_digest_seen row, count the Features changed since then, and fill each group's rows and counts
    // (health, status line, rollups, INeedsYouCounts, rank, Epic). Until then: never seen, nothing changed, four empty groups.
    public Task<FeaturesOverviewDto> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var (_, sinceLabel) = FeaturesSince.Of(null, clock);
        return Task.FromResult(new FeaturesOverviewDto(
            projectId,
            sinceLabel,
            0,
            [
                new FeatureGroupDto("inProgress", "Active", false, 0, []),
                new FeatureGroupDto("resolved", "Resolved", false, 0, []),
                new FeatureGroupDto("proposed", "New", false, 0, []),
                new FeatureGroupDto("completed", "Closed", true, 0, []),
            ]));
    }
}
