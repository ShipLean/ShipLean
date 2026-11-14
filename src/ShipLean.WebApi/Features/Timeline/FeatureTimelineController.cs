using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Timeline;

[Route("api/projects/{projectId:guid}/work-items/by-key/{itemKey}/timeline")]
public sealed class FeatureTimelineController(IFeatureTimeline timeline) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<FeatureTimelineViewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<FeatureTimelineViewDto> Get(Guid projectId, string itemKey, CancellationToken cancellationToken)
        => timeline.GetAsync(projectId, itemKey, cancellationToken);
}
