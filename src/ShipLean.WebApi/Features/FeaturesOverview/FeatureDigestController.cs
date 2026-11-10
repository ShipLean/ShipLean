using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.FeaturesOverview;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Screens;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.FeaturesOverview;

[Route("api/projects/{projectId:guid}/features/digest")]
public sealed class FeatureDigestController(IFeatureDigest digest, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<FeatureDigestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<FeatureDigestDto> Get(Guid projectId, [FromQuery] string? cursor, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        return await digest.GetAsync(projectId, cursor, cancellationToken);
    }

    [HttpPost("seen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> MarkSeen(Guid projectId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        await digest.MarkSeenAsync(projectId, cancellationToken);
        return NoContent();
    }
}
