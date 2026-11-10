using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.FeaturesOverview;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Screens;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.FeaturesOverview;

[Route("api/projects/{projectId:guid}/features/overview")]
public sealed class FeaturesOverviewController(IFeaturesOverview overview, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<FeaturesOverviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<FeaturesOverviewDto> Get(Guid projectId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        return await overview.GetAsync(projectId, cancellationToken);
    }
}
