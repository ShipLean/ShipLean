using ShipLean.Contracts.Screens;

namespace ShipLean.Application.Features.FeaturesOverview;

// The phone's Features overview of a project for the current user: what changed since their last digest visit, and the Features
// grouped Active, Resolved, New, then Closed (folded). Callers check Member access first.
public interface IFeaturesOverview
{
    Task<FeaturesOverviewDto> GetAsync(Guid projectId, CancellationToken cancellationToken);
}
