using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Sprints.Metrics;

// The PUT on the same route (the velocity override) is ForecastController's.
[Route("api/projects/{id:guid}/forecast")]
public sealed class ProjectForecastController(ISprintMetrics metrics, IProjectAccess access) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ForecastDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<ForecastDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return await metrics.GetForecastAsync(id, cancellationToken);
    }
}
