using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Taskboard.FeatureBoard;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Views;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Taskboard;

[Route("api/projects/{projectId:guid}/work-items/by-key/{itemKey}/feature-board")]
public sealed class FeatureBoardReadController(FeatureBoardViews views) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<FeatureBoardViewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<FeatureBoardViewDto> Get(Guid projectId, string itemKey, CancellationToken cancellationToken)
        => views.GetAsync(projectId, itemKey, cancellationToken);
}
