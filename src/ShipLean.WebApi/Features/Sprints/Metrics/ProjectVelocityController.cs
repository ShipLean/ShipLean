using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Sprints.Metrics;

[Route("api/projects/{id:guid}/velocity")]
public sealed class ProjectVelocityController(ISprintMetrics metrics, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<VelocityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<VelocityDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await metrics.GetVelocityAsync(id, cancellationToken);
    }
}
